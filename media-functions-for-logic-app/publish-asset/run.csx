/*
This function publishes an asset.

Input:
{
    "assetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Mandatory, Id of the source asset
    "preferredSE" : "default" // Optional, name of Streaming Endpoint if a specific Streaming Endpoint should be used for the URL outputs
}

Output:
{
    playerUrl : "", // Url of demo AMP with content
    smoothUrl : "", // Url for the published asset (contains name.ism/manifest at the end) for dynamic packaging
    pathUrl : ""    // Url of the asset (path)
}
*/

#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
//#r "Microsoft.Azure.Documents.Client"
//#r "Microsoft.Azure.DocumentDB"
#load "../Shared/mediaServicesHelpers.csx"
#load "../Shared/copyBlobHelpers.csx"

using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Web;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.Azure.WebJobs;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents;

// Read values from the App.config file.
static string _storageAccountName = Environment.GetEnvironmentVariable("MediaServicesStorageAccountName");
static string _storageAccountKey = Environment.GetEnvironmentVariable("MediaServicesStorageAccountKey");

static readonly string _AADTenantDomain = Environment.GetEnvironmentVariable("AMSAADTenantDomain");
static readonly string _RESTAPIEndpoint = Environment.GetEnvironmentVariable("AMSRESTAPIEndpoint");

static readonly string _mediaservicesClientId = Environment.GetEnvironmentVariable("AMSClientId");
static readonly string _mediaservicesClientSecret = Environment.GetEnvironmentVariable("AMSClientSecret");
static readonly string _cosmosUrl = Environment.GetEnvironmentVariable("CosmosDBUrl");
static readonly string _cosmosKey = Environment.GetEnvironmentVariable("CosmosDBKey");

// Field for service context.
private static CloudMediaContext _context = null;
private static CloudStorageAccount _destinationStorageAccount = null;

public static async Task<object> Run(HttpRequestMessage req, TraceWriter log, Microsoft.Azure.WebJobs.ExecutionContext execContext)
{
    log.Info($"Webhook was triggered!");

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);

    log.Info(jsonContent);

    if (data.assetId == null)
    {
        // for test
        // data.assetId = "nb:cid:UUID:c0d770b4-1a69-43c4-a4e6-bc60d20ab0b2";
        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass asset ID in the input object (assetId)"
        });
    }

    string thumbnailUrl = "";
    string playerUrl = "";
    string smoothUrl = "";
    string pathUrl = "";
    string preferredSE = data.preferredSE;
    string documentId = "";
    string publishedDocumentId = "";
    string videoName = "";

    log.Info($"Using Azure Media Service Rest API Endpoint : {_RESTAPIEndpoint}");

    DocumentClient client = new DocumentClient(new Uri(_cosmosUrl), _cosmosKey);  

    try
    {
        AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(_AADTenantDomain,
                            new AzureAdClientSymmetricKey(_mediaservicesClientId, _mediaservicesClientSecret),
                            AzureEnvironments.AzureCloudEnvironment);

        AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

        _context = new CloudMediaContext(new Uri(_RESTAPIEndpoint), tokenProvider);

        // Get the asset
        string assetid = data.assetId;
        var outputAsset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

        if (outputAsset == null)
        {
            log.Info($"Asset not found {assetid}");

            return req.CreateResponse(HttpStatusCode.BadRequest, new
            {
                error = "Asset not found"
            });
        }

        //Get Thumbnail URL
        var readPolicy =
            _context.AccessPolicies.Create("readPolicy", TimeSpan.FromHours(4), AccessPermissions.Read);
        var outputLocator = _context.Locators.CreateLocator(LocatorType.Sas, outputAsset, readPolicy);
        var thumbnailFile = outputAsset.AssetFiles.AsEnumerable().Where(f => f.Name.EndsWith(".png")).OrderByDescending(f => f.IsPrimary).FirstOrDefault();

        if(thumbnailFile != null && !string.IsNullOrEmpty(thumbnailFile.Name))
        { 
            thumbnailUrl = string.Format("{0}/{1}{2}", outputAsset.Uri.ToString(), thumbnailFile.Name, outputLocator.ContentAccessComponent);
        }

        videoName = outputAsset.Name;
        var result = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri("Media", "Assets"), outputAsset);
        documentId = result.Resource.Id;

        // publish with a streaming locator (100 years)
        IAccessPolicy readPolicy2 = _context.AccessPolicies.Create("readPolicy", TimeSpan.FromDays(365*100), AccessPermissions.Read);
        ILocator outputLocator2 = _context.Locators.CreateLocator(LocatorType.OnDemandOrigin, outputAsset, readPolicy2);

        var publishurlsmooth = GetValidOnDemandURI(outputAsset, preferredSE);
        var publishurlpath = GetValidOnDemandPath(outputAsset, preferredSE);

        if (outputLocator2 != null && publishurlsmooth != null)
        {
            smoothUrl = publishurlsmooth.ToString();
            playerUrl = "https://ampdemo.azureedge.net/?url=" + System.Web.HttpUtility.UrlEncode(smoothUrl);
            log.Info($"smooth url : {smoothUrl}");
        }

        if (outputLocator2 != null && publishurlpath != null)
        {
            pathUrl = publishurlpath.ToString();
            log.Info($"path url : {pathUrl}");
        }
    }

    catch (Exception ex)
    {
        log.Info($"Exception {ex}");
        return req.CreateResponse(HttpStatusCode.InternalServerError, new
        {
            Error = ex.ToString()
        });
    }

    try
    {      
        var result = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri("Media", "PublishedAssets"), new
        {
            thumbnailUrl = thumbnailUrl,
            videoName = videoName,
            playerUrl = playerUrl,
            smoothUrl = smoothUrl,
            pathUrl = pathUrl
        });
        publishedDocumentId = result.Resource.Id;
    }
    catch (Exception ex)
    {
        log.Info($"Exception {ex}");
        return req.CreateResponse(HttpStatusCode.InternalServerError, new
        {
            Error = ex.ToString()
        });
    }

    log.Info($"");
    return req.CreateResponse(HttpStatusCode.OK, new
    {
        thumbnailUrl = thumbnailUrl,
        videoName = videoName,
        playerUrl = playerUrl,
        smoothUrl = smoothUrl,
        pathUrl = pathUrl,
        documentId = documentId,
        publishedDocumentId = publishedDocumentId
    });
}
