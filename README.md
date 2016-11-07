---
services: media-services,functions
platforms: dotnet
author: johndeu
---

# Media Services: Integrating Azure Media Services with Azure Functions
This project contains examples of using Azure Functions with Azure Media Services. 
The project includes several folders of sample Azure Functions for use with Azure Media Services that show workflows related
to ingesting content directly from blob storage, encoding, and writing content back to blob storage. It also includes examples of
how to monitor job notifications via WebHooks and Azure Queues. 

## How to run the sample

To run the samples, simply fork this project into your own repository and attach your Github account with a new
Azure Functions application. 

To configure the sample Functions, you need to set the following values in your
function's Application Settings.

* **AMSAccount** - your Media Services Account name. 
* **AMSKey** - your Media Services key. 
* **MediaServicesStorageAccountName** - the storage account name tied to your Media Services account. 
* **MediaServicesStorageAccountKey** - the storage account key tied to your Media Services account. 
* **StorageConnection** -  the functions.json file contains a "StorageConnection" property which must be set to an App Setting value that 
  contains a connection string for your input storage account. Otherwise, you may end up with an error message at startup.
  Make sure to add a new AppSetting to your Functions project with the storage account name and connection string, and update
  the functions.json file if you see this error:

 `Microsoft.Azure.WebJobs.Host: Error indexing method 'Functions.EncodeBlob_MultiOut_Function'. Microsoft.Azure.WebJobs.Host: Value cannot be null.
  Parameter name: dataAccount.`

  To find the connection string for your storage account, open the storage account in the 
  Azure portal(Ibiza). Go to Access Keys in Settings. In the Access Keys blade
  go to Key1, or Key2, click the "..." menu and select "view connection string". Copy the connection string.
  
* The output container name can be modifed in run.csx by changing the value of the static string _outputContainerName.
  It's set to "output" by default. 

## EncodeBlob_SingleOut_Function

This function demonstrates how to use WebHooks to listen to a basic encoding job's progress.  

You will need to update the <<YOUR_FUNCTION_APP>> and <<YOUR_FUNCTION_KEY>> to point to the right URL for the Notification_Webhook_Function.

        string webhookEndpoint = @"https://<<YOUR_FUNCTION_APP>>.azurewebsites.net/api/Notification_Webhook_Function?code=<<YOUR_FUNCTION_KEY>>";

This Function waits for content to be copied into an input container 
tht is configured in the function.json file's bindings.

    {
        "name": "inputBlob",
        "type": "blobTrigger",
        "direction": "in",
        "path": "input/{fileName}.{fileExtension}",
        "connection": "StorageConnection"
    }

The name property sets the name of the CloudBlockBlob property that is passed into the Run method. 
The path property sets the container name and file matching pattern to use. In this example,
we set the {fileName} and {fileExtension} matching patterns to pass the two values into the Run function.

    public static void Run(CloudBlockBlob inputBlob, TraceWriter log, string fileName, string fileExtension)

### EncodeBlobFunction Workflow
The function will execute the following workflow:

1. Watch the "input" container for new files and copy the blob  into a new Media Services Asset (IAsset).
2. Create the required AssetFiles for the new Asset and set the copied blob to be the primary file in the Asset.
3. Create a new encoding job using a preset .json for 720p Adaptive bitrate encoding. 
4. Wait for the encoding job to finish. 
5.Copy all of the output files from the job into an "output" container in the same storage account as the input blob.


## EncodeBlob_SingleOut_Function
The EncodeBlob_SingleOut_Function demonstrates how to use an Output binding and the "InOut" direction binding to 
allow the Azure functions framework to create the output blob for you automatically. 

In the function.json, you will notice that we use a binding direction of "InOut" and also set the name to "outputBlob".
The path is also updated to point to a specific output container, and a pattern is provided for naming the output file. 
Notice that we are binding the input {filename} to the output {filename} pattern match, and also specifying a default
extension of "-Output.mp4". 

    {
      "name": "outputBlob",
      "type": "blob",
      "direction": "InOut",
      "path": "output/{fileName}-Output.mp4",
      "connection": "StorageConnection"
    }

In the run.csx file, we then bind this outputBlob to the Run method signature as a CloudBlockBlob. 

    public static void Run( CloudBlockBlob inputBlob, 
                            string fileName, 
                            string fileExtension, 
                            CloudBlockBlob outputBlob, 
                            TraceWriter log)

To output data to this outputBlob, we have to copy data into it. The CopyBlob() helper method (in 'Shared/copyBlobHelpers.csx') is used to copy the stream 
from the source blob to the output blob. Since the copy is done async, we have to call Wait() and halt the function execution until the copy is complete.

    CopyBlob(jobOutput,outputBlob).Wait();

Finally, we can set a few properties on the outputBlob before the function returns, and the blob is written to the configured 
output storage account set in the function.json binding.

          
    // Change some settings on the output blob.
    outputBlob.Metadata["Custom1"] = "Some Custom Metadata";
    outputBlob.Properties.ContentType = "video/mp4";
    outputBlob.SetProperties();

## EncodeBlob_MultiOut_Function

This function can call a Logic App at the end.
Specify the call back Url in **LogicAppCallbackUrl** in your function's Application Settings.


## EncodeBlob_MultiOut_MultiFilesInput_Function (Multiple files / single asset Function)
This function will upload several files into a single asset.
A json file must be uploaded to the blob container withh the referenced files.

The format of the json file is:

    [
      {
        "fileName": "BigBuckBunny.mp4",
        "isPrimary": true
      },
      {
        "fileName": "Logo.png"
      }
    ]


### License
This sample project is licensed under [the MIT License](LICENSE.txt)

## ToDO 
- [ ] The Azure Queue notification function is not yet complete
- [ ] Copy Blobs currently is using Streams, and copies in an inefficient way.
