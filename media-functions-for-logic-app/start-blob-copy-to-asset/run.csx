﻿/*
This function copy a file (blob) to a new asset previously created.

Input:
{
    "assetId" : "the Id of the asset where the file must be copied",
    "fileName" : "filename.mp4",
    "sourceStorageAccountName" : "",
    "sourceStorageAccountKey": "",
    "sourceContainer" : ""
}
Output:
{
 "destinationContainer": "" // container of asset
}

*/

#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"

#load "../Shared/copyBlobHelpers.csx"
#load "../Shared/ingestAssetConfigHelpers.csx"
#load "../Shared/mediaServicesHelpers.csx"

using System;
using System.Net;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;

// Read values from the App.config file.
private static readonly string _mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
private static readonly string _mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");
private static readonly string _storageAccountName = Environment.GetEnvironmentVariable("MediaServicesStorageAccountName");
private static readonly string _storageAccountKey = Environment.GetEnvironmentVariable("MediaServicesStorageAccountKey");

// Field for service context.
private static CloudMediaContext _context = null;
private static CloudStorageAccount _destinationStorageAccount = null;

public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"Webhook was triggered!");

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);
    log.Info("Request : " + jsonContent);

    // Validate input objects
      if (data.assetId == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass assetId in the input object" });
 
    if (data.fileName == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass fileName in the input object" });
    if (data.sourceStorageAccountName == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass sourceStorageAccountName in the input object" });
    if (data.sourceStorageAccountKey == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass sourceStorageAccountKey in the input object" });
          if (data.sourceContainer == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass sourceContainer in the input object" });
   
    log.Info("Input - fileName : " + data.fileName);
     log.Info("Input - sourceStorageAccountName : " + data.sourceStorageAccountName);
    log.Info("Input - sourceStorageAccountKey : " + data.sourceStorageAccountKey);
       log.Info("Input - sourceContainer : " + data.sourceContainer);

string fileName=(string) data.fileName;
    string _sourceStorageAccountName = data.sourceStorageAccountName;
    string _sourceStorageAccountKey = data.sourceStorageAccountKey;
    string assetId = data.assetId;

    IAsset newAsset = null;
    IIngestManifest manifest = null;
    try
    {
        // Load AMS account context
        log.Info("Using Azure Media Services account : " + _mediaServicesAccountName);
        _context = new CloudMediaContext(new MediaServicesCredentials(_mediaServicesAccountName, _mediaServicesAccountKey));

        // Find the Asset
        newAsset = _context.Assets.Where(a=> a.Id == assetId).FirstOrDefault();
 if (newAsset == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Asset not found" });

         // Setup blob container
        CloudBlobContainer sourceBlobContainer = GetCloudBlobContainer(_sourceStorageAccountName, _sourceStorageAccountKey, (string)data.sourceContainer);
        CloudBlobContainer destinationBlobContainer = GetCloudBlobContainer(_storageAccountName, _storageAccountKey, newAsset.Uri.Segments[1]);
        sourceBlobContainer.CreateIfNotExists();
        // Copy Source Blob container into Destination Blob container that is associated with the asset.
        //CopyBlobsAsync(sourceBlobContainer, destinationBlobContainer, log);
         
              CloudBlob sourceBlob = sourceBlobContainer.GetBlockBlobReference(fileName);
         CloudBlob destinationBlob = destinationBlobContainer.GetBlockBlobReference(fileName);
 
 if (destinationBlobContainer.CreateIfNotExists())
    {
        destinationBlobContainer.SetPermissions(new BlobContainerPermissions
        {
            PublicAccess = BlobContainerPublicAccessType.Blob
        });
    }

         CopyBlobAsync(sourceBlob , destinationBlob);
    }
    catch (Exception ex)
    {
        log.Info("Exception " + ex);
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

 
    return req.CreateResponse(HttpStatusCode.OK, new
    {
        destinationContainer = newAsset.Uri.Segments[1]
    });
}
