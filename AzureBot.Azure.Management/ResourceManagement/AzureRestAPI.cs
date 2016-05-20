using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AzureBot.Azure.Management.ResourceManagement
{
    public class AzureRestAPI
    {
        private string doGET(string URI, String token)
        {
            Uri uri = new Uri(String.Format(URI));

            // Create the request
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + token);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "GET";

            // Get the response
            HttpWebResponse httpResponse = null;
            httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            string result = null;
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }
            return result;
        }
        private string doPUT(string URI, string body, String token)
        {
            Uri uri = new Uri(String.Format(URI));

            // Create the request
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + token);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "PUT";

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                streamWriter.Write(body);
                streamWriter.Flush();
                streamWriter.Close();
            }

            // Get the response
            HttpWebResponse httpResponse = null;
            httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

            string result = null;
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }
            return result;
        }
    }
}
