using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml.XPath;

namespace Cabfind.DespatchManager
{
    public class GoogleGeocode
    {

        public static string[] getLatLng(string adress)
        {
            string[] resuts = new string[] {"0", "0"};
            string url = "http://maps.googleapis.com/maps/api/geocode/" + "xml?address=" + adress + "&sensor=false";
            WebResponse response = null;
            try
            {
                HttpWebRequest request = (HttpWebRequest) WebRequest.Create(url);
                request.Method = "GET";
                response = request.GetResponse();
                if (response != null)
                {
                    XPathDocument document = new XPathDocument(response.GetResponseStream());
                    XPathNavigator navigator = document.CreateNavigator();
                    // get response 
                    //status
                    XPathNodeIterator statusIterator = navigator.Select("/GeocodeResponse/status");
                    while (statusIterator.MoveNext())
                    {
                        if (statusIterator.Current.Value != "OK")
                        {
                            return resuts;
                        }
                    }
                    // get results
                    XPathNodeIterator resultIterator = navigator.Select("/GeocodeResponse/result");
                    if (resultIterator.MoveNext())
                    {
                        XPathNodeIterator geometryIterator = resultIterator.Current.Select("geometry");
                        if (geometryIterator.MoveNext())
                        {
                            XPathNodeIterator locationIterator = geometryIterator.Current.Select("location");
                            if (locationIterator.MoveNext())
                            {
                                XPathNodeIterator latIterator = locationIterator.Current.Select("lat");
                                if (latIterator.MoveNext())
                                {
                                    resuts[0] = latIterator.Current.Value;
                                }
                                XPathNodeIterator lngIterator = locationIterator.Current.Select("lng");
                                if (lngIterator.MoveNext())
                                {
                                    resuts[1] = lngIterator.Current.Value;
                                }
                            }
                        }
                    }
                    return resuts;
                }
            }
            catch (Exception ex)
            {
                
            }
            return resuts;
        }
    }
}


 


