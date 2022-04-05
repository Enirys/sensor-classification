using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using RestClient.Core;
using RestClient.Core.Models;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class AzureComputerVisionClient : MonoBehaviour
{
    [SerializeField]
    private string baseUrl = "https://enirysvision-prediction.cognitiveservices.azure.com/customvision/v3.0/Prediction/";

    [SerializeField]
    private string clientId;

    [SerializeField]
    private string clientSecret;
    
    [SerializeField]
    private string projectId;
    
    [SerializeField]
    private string iterationName;

    [SerializeField] private bool isUrl;

    private string _imageToPredict = "";

    [SerializeField] 
    private TMP_Text predictionTxt;
    [SerializeField] 
    private TMP_Text confidenceTxt;
    [SerializeField] 
    private RawImage previewImage;
    [SerializeField]
    private TMP_InputField imageUrlField;
    [SerializeField]
    private Slider confidenceSlider;
    [SerializeField]
    private GameObject predictionResult;

    private RequestHeader _clientSecurityHeader;
    private RequestHeader _contentTypeHeader;
    private string _selectedPath = "";
    
    void Start()
    {
        // setup the request header
        _clientSecurityHeader = new RequestHeader {
            Key = clientId,
            Value = clientSecret
        };

        // setup the request header
        _contentTypeHeader = new RequestHeader {
            Key = "Content-Type",
            Value = isUrl ? "application/json" : "application/octet-stream"
        };
    }
    
    public void PickImage()
    {
        NativeGallery.Permission permission = NativeGallery.GetImageFromGallery((path) =>
        {
            _selectedPath = path;
            Debug.Log( "Image path: " + _selectedPath );
            StartCoroutine(DownloadImage(_selectedPath));
        });

        Debug.Log( "Permission result: " + permission );
    }
    
    public void PredictFromFile()
    {
        // send a post request
        StartCoroutine(RestWebClient.Instance.HttpPostBinary($"{baseUrl}/{projectId}/classify/iterations/{iterationName}/image", _selectedPath, (r) => OnRequestComplete(r), new List<RequestHeader> 
        {
            _clientSecurityHeader,
            _contentTypeHeader
        }));
    }
    
    IEnumerator DownloadImage(string MediaUrl)
    {   
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(MediaUrl);
        yield return request.SendWebRequest();
        if(request.isNetworkError || request.isHttpError) 
            Debug.Log(request.error);
        else
            previewImage.texture = ((DownloadHandlerTexture) request.downloadHandler).texture;
    } 

    public void PredictFromURL()
    {
        // validation
        if(string.IsNullOrEmpty(imageUrlField.text))
        {
            Debug.LogError("imageToPredict needs to be set through the inspector...");
            return;
        }
        
        _imageToPredict = imageUrlField.text;
        StartCoroutine(DownloadImage(_imageToPredict));

        // build image url required by Azure Custom Vision
        ImageUrl imageUrl = new ImageUrl { Url = _imageToPredict };
        
        // send a post request
        StartCoroutine(RestWebClient.Instance.HttpPost($"{baseUrl}/{projectId}/classify/iterations/{iterationName}/url", JsonUtility.ToJson(imageUrl), (r) => OnRequestComplete(r), new List<RequestHeader> 
        {
            _clientSecurityHeader,
            _contentTypeHeader
        }));
    }

    void OnRequestComplete(Response response)
    {
        Debug.Log($"Status Code: {response.StatusCode}");
        Debug.Log($"Data: {response.Data}");
        Debug.Log($"Error: {response.Error}");
        
        if(string.IsNullOrEmpty(response.Error) && !string.IsNullOrEmpty(response.Data))
        {
            AzureCustomVisionResponse azureCustomVisionResponse = JsonUtility.FromJson<AzureCustomVisionResponse>(response.Data);
            
            // show the prediction with the highest probability
            confidenceTxt.text = "Confidence " + (azureCustomVisionResponse.predictions[0].probability * 100).ToString("0.00") + "%";
            string predictionName = azureCustomVisionResponse.predictions[0].tagName;
            predictionTxt.text = predictionName;
            confidenceSlider.value = azureCustomVisionResponse.predictions[0].probability;
            predictionResult.SetActive(true);
            string[] modelName = predictionName.Split(' ');
            string reference = modelName[modelName.Length - 1];
            HomeMenuController.Instance.SensorModel = Resources.Load("3DModels/Prefabs/" + reference) as GameObject;
        }
    }

    public class ImageUrl 
    {
        public string Url;
    }
}
