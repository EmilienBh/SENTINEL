using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

public static class GoogleDriveCircuitDataUploader {


    const string driveID = "1sGEAEJ2AW8mrJ9qJvQtYSNAEQrKvSRGp"; // Remplacer par l'ID du dossier Google Drive à utiliser

    private static DriveService service;
    private static string fileName; // The file name, which will be based on this session start date and time
    private static string fileID; // The file where the logs of this session are uploaded (null while not created)
    private static string fileContent = ""; // The file content, to increment over this session


    public static void SubmitCircuitData(string dataToString) {
        fileContent += dataToString + "\n\n";
        UploadFileFromText(fileContent);
    }

    private static bool AuthenticateWithServiceAccount() {
        TMP_Logs.PrintLog("Attempting Authentication...");
        try {
            string serviceAccountKeyFilePath = Path.Combine("Assets/service-account-key.json"); // Clé JSON téléchargée
            ServiceAccountCredential credential;
            /*
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes())) {
                TMP_Logs.PrintLog("Test : Return 1 : " + GetServiceAccountKey());
                ServiceAccountKeyLoader
                var gCreds = GoogleCredential.FromStream(stream);
                TMP_Logs.PrintLog("Test : Return 1.5 : "+ gCreds);
                credential = gCreds.CreateScoped(DriveService.Scope.DriveFile);
                TMP_Logs.PrintLog("Test : Return 2.");
            }*/

            service = GoogleDriveServiceManualAuth.InitializeDriveService(GetServiceAccountKey());

            fileName = $"Circuit Data - {DateTime.Now.ToString()}.txt";
            Debug.Log("Authenticated using Service Account.");
            return true;
        }
        catch (Exception e) {
            Debug.LogError("Error while trying to authenticate using Service Account : " + e.Message);
            //TMP_Logs.PrintLog("Error while trying to authenticate using Service Account : " + e.Message);
            return false;
        }
    }

    public class GoogleDriveServiceManualAuth {
        public class ServiceAccountKey {
            public string type;
            public string project_id;
            public string private_key_id;
            public string private_key;
            public string client_email;
            public string client_id;
            public string auth_uri;
            public string token_uri;
            public string auth_provider_x509_cert_url;
            public string client_x509_cert_url;
        }

        public static DriveService InitializeDriveService(string serviceAccountJson) {
            try {
                // Désérialiser avec JsonUtility
                var serviceAccountKey = JsonUtility.FromJson<ServiceAccountKey>(serviceAccountJson);
                if (serviceAccountKey == null) {
                    UnityEngine.Debug.LogError("Failed to deserialize service account JSON.");
                    return null;
                }

                // Créer une source d’identités basée sur la clé privée
                var credential = new ServiceAccountCredential(
                    new ServiceAccountCredential.Initializer(serviceAccountKey.client_email) {
                        Scopes = new[] { DriveService.Scope.DriveFile }
                    }.FromPrivateKey(serviceAccountKey.private_key)
                );

                // Initialiser le service Google Drive
                var service = new DriveService(new BaseClientService.Initializer() {
                    HttpClientInitializer = credential,
                    ApplicationName = "Your Application Name",
                });

                UnityEngine.Debug.Log("Google Drive service initialized successfully.");
                return service;
            }
            catch (Exception ex) {
                UnityEngine.Debug.LogError($"Failed to initialize Google Drive service: {ex.Message}");
                return null;
            }
        }
    }

    private static string GetServiceAccountKey() {
        string path = Path.Combine(Application.streamingAssetsPath, "service-account-key.json");

        if (path.Contains("://")) { // Android platform
            UnityWebRequest www = UnityWebRequest.Get(path);
            www.SendWebRequest();
            while (!www.isDone) { } // Attendez que le fichier soit chargé
            if (www.result == UnityWebRequest.Result.Success) {
                return www.downloadHandler.text;
            }
            else {
                Debug.LogError("Failed to load JSON from StreamingAssets: " + www.error);
                return null;
            }
        }
        else { // Other platforms
            return File.ReadAllText(path);
        }
    }

    /*
     * Updload the circuit logs, procided as a string to encode as text file
     */
    private static async void UploadFileFromText(string fileContent) {
        if (service == null) {
            if (!AuthenticateWithServiceAccount()) {
                Debug.LogError("Google Drive service not initialized, can't upload.");
                return;
            }
        }

        byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(fileContent);
        using (var stream = new MemoryStream(textBytes)) {
            if (fileID == null) {
                await CreateFileFromText(stream);
            }
            else {
                await UpdateFileFromText(stream);
            }
        }
    }

    private static async Task CreateFileFromText(MemoryStream stream) {
        var fileMetadata = new Google.Apis.Drive.v3.Data.File() {
            Name = fileName,
            Parents = new List<string> { driveID }
        };

        var request = service.Files.Create(fileMetadata, stream, "text/plain");
        //request.Fields = "id";
        var progress = await request.UploadAsync();

        if (progress.Status == UploadStatus.Completed) {
            fileID = request.ResponseBody.Id;
            Debug.Log("File uploaded successfully.");
            TMP_Logs.PrintLog("Create - File uploaded successfully.");
        }
        else {
            Debug.LogError("File upload failed: " + progress.Exception);
            TMP_Logs.PrintLog("Create - File upload failed: " + progress.Exception);
        }
    }

    private static async Task UpdateFileFromText(MemoryStream stream) {
        var fileMetadata = new Google.Apis.Drive.v3.Data.File() {
            Name = fileName
        };

        var request = service.Files.Update(fileMetadata, fileID, stream, "text/plain");
        request.Fields = "id";
        var progress = await request.UploadAsync();

        if (progress.Status == UploadStatus.Completed) {
            fileID = request.ResponseBody.Id;
            Debug.Log("File uploaded successfully.");
            TMP_Logs.PrintLog("Update - File uploaded successfully.");
        }
        else {
            Debug.LogError("File upload failed: " + progress.Exception);
            TMP_Logs.PrintLog("Update - File upload failed: " + progress.Exception);
        }
    }

}
