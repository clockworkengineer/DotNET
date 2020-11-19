//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Perform a web request. This simple is wrapper class 
// for the http requests that the HTTP tracker makes.
//
// Copyright 2020.
//
using System.IO;
using System.Net;
namespace BitTorrentLibrary
{
    public interface IWeb
    {
        byte[] ResponseData { get; set; }
        string StatusDescription { get; set; }
        bool Get();
        void SetURL(string url);
    }
    public class Web : IWeb
    {
        private HttpWebRequest httpGetRequest;
        public byte[] ResponseData { get; set; }
        public string StatusDescription { get; set; }
        /// <summary>
        /// Setup data and resources needed by Web.
        /// </summary>
        public Web()
        {
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        public void SetURL(string url)
        {
            httpGetRequest = WebRequest.Create(url) as HttpWebRequest;
            httpGetRequest.Method = "GET";
            httpGetRequest.ContentType = "text/xml";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
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