//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Simple adper class for the requests that the 
// HTTP tracker makes.
//
// Copyright 2020.
//
using System.IO;
using System.Net;
namespace BitTorrentLibrary
{
    internal interface IWeb
    {
        byte[] ResponseData { get; set; }
        string StatusDescription { get; set; }
        bool Get();
        void SetURL(string url);
    }
    internal class Web : IWeb
    {
        private HttpWebRequest httpGetRequest;          // HTTP request object
        public byte[] ResponseData { get; set; }        // Request response data
        public string StatusDescription { get; set; }   // Returned status description
        /// <summary>
        /// Setup data and resources needed by Web.
        /// </summary>
        public Web()
        {
        }
        /// <summary>
        ///  Intialise web request.
        /// </summary>
        /// <param name="url"></param>
        public void SetURL(string url)
        {
            httpGetRequest = WebRequest.Create(url) as HttpWebRequest;
            httpGetRequest.Method = "GET";
            httpGetRequest.ContentType = "text/xml";
        }
        /// <summary>
        /// Make web request and receive response.
        /// </summary>
        /// <returns>true on sucess</returns>
        public bool Get()
        {
            bool success = false;
            using (HttpWebResponse httpGetResponse = httpGetRequest.GetResponse() as HttpWebResponse)
            {
                StreamReader reader = new StreamReader(httpGetResponse.GetResponseStream());
                using (var memstream = new MemoryStream())
                {
                    reader.BaseStream.CopyTo(memstream);
                    ResponseData = memstream.ToArray();
                }
                StatusDescription = httpGetResponse.StatusDescription;
                success = (httpGetResponse.StatusCode == HttpStatusCode.OK);
            }
            return success;
        }
    }
}