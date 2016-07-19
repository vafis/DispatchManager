using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web;
using Booking;
using System.Net;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Configuration;
using Cabfind.DespatchManager.AurigaWS;
using System.IO;


namespace Cabfind.DespatchManager.Despatchers
{
    public class Cordic : IDespatchStrategy
    {
        enum ErrorCode
        {
            OK = 1
        }

        private string _xmlFolder = System.Text.RegularExpressions.Regex.Split(AppDomain.CurrentDomain.BaseDirectory.Replace("/", "\\"), @"bin")[0] + "CordicXML\\";
        private string _ns = "http://www.cordicgateway.com/cni";
        private IEnumerable<System.Xml.Linq.XElement> _elements;
        private XDocument _soapDocument;
        private XNamespace rootNamespace;

        private Func<DateTime, string> checkDayLightSave = (dt) =>
        {
            bool ret = dt.IsDayLightSaveDateTime();
            return ret == true ? "+01:00" : "";
        };

        private void LoadXml(string xmlFile)
        {
            string filepath = _xmlFolder + xmlFile;
            _soapDocument = XDocument.Load(filepath);
            rootNamespace = _soapDocument.Root.Name.NamespaceName;
        }

        private void InitRequest(clsWebSupplierDespatch suppDesp)
        {
            XElement rootElement = _soapDocument.Root;
            _elements  = from elements in rootElement.Descendants()
                        select elements;
            _elements.Single(x => x.Name.LocalName == "SourceSystem").Value = suppDesp.CordicSourceSystem;
            _elements.Single(x => x.Name.LocalName == "SourcePassword").Value = suppDesp.CordicSourcePassword;
            
        }

        private void MapBooking(clsWebSupplierBooking booking)
        {
            XNamespace ns = _elements.Single(x => x.Name.LocalName == "JobRequest").Name.Namespace;
            int i = 1;
            //Pickup

            _elements.Single(x => x.Name.LocalName == "StopList").Add(new XElement(ns + "Stop", new XElement(ns + "Order", i.ToString()),
                                                                                           new XElement(ns + "Passenger", booking.ClientLeadName),
                                                                                           new XElement(ns + "Address", booking.PickupAddressOneLine),
                                                                                           new XElement(ns + "Postcode", booking.PickupAddressPostcodeFirstPart + " " + booking.PickupAddressPostcodeSecondPart),
                                                                                           new XElement(ns + "ContactPhone", booking.PassengerTelNo),
                                                                                           new XElement(ns + "ContactOnArrive", booking.CallOnArrival == "True" ? "Ring" : ""))
                                                                                           );
           
            if (booking.PickupBookingFlightID > 0)
            {
                _elements.Single(x => x.Name.LocalName == "Stop")
                    .Add(new XElement(ns + "FlightDetails",
                        new XElement(ns + "FlightCode", booking.PickupFlightNo),
                        new XElement(ns + "Terminal", booking.PickupTerminal),
                        new XElement(ns + "ArrivalTime", booking.PickupFlightTime.ToString())));
            }


            foreach (var via in booking.BookingVias.Vias)
            {
                i++;
                _elements.Single(x => x.Name.LocalName == "StopList").Add(new XElement(ns + "Stop",
                                                                          new XElement(ns + "Order", i.ToString()),
                                                                          new XElement(ns + "Address", via.Address.ToString()),
                                                                          new XElement(ns + "Postcode", via.Address.OutCode + " " + via.Address.InCode)
                                                                          ));
            }

            i++;

            _elements.Single(x => x.Name.LocalName == "StopList").Add(new XElement(ns + "Stop",
                                                                      new XElement(ns + "Order", i.ToString()),
                                                                      new XElement(ns + "Address", booking.DestAddressOneLine),
                                                                      new XElement(ns + "Postcode", booking.DestAddressPostcodeFirstPart + " " + booking.DestAddressPostcodeSecondPart)
                                                                      ));

            //Set a 10 min window in order Cordic accept booking
            _elements.Single(x => x.Name.LocalName == "PickupTime").Value =
                booking.PickupDateTime > DateTime.Now.AddMinutes(10.00)
                ? booking.PickupDateTime.ToString("s") //+ checkDayLightSave(booking.PickupDateTime) 
                    : "";
           // _elements.Single(x => x.Name.LocalName == "PickupTime").Value = booking.PickupDateTime.ToUniversalTime().ToString("s");
            _elements.Single(x => x.Name.LocalName == "DriverNotes").Value = ProcessDriverNotes(booking);
            _elements.Single(x => x.Name.LocalName == "Attribute").Value = booking.WheelChair == "True" | booking.CordicSupplierVehicle.ToString().Trim() == "WheelChair"
                ? "Wheelchair"
                : booking.CordicSupplierVehicle.ToString().Trim();
            // _soapDocument.Root.Element("AttributeList").Add(new XElement("Attribute", booking.WheelChair == "True" ? "Wheelchair" : booking.CordicSupplierVehicle));  
            //   _elements.Single(x => x.Name.LocalName == "AttributeList").Add(new XStreamingElement("Attribute", booking.WheelChair == "True" ? "Wheelchair" : booking.CordicSupplierVehicle));
            // _elements.Single(x => x.Name.LocalName == "AttributeList").Add(new XElement("Attribute", booking.CordicSupplierVehicle));
            //_soapDocument.Root.Element("AttributeList").Add(new XAttribute("Attribute", booking.WheelChair == "True"?"Wheelchair": booking.CordicSupplierVehicle));

            // flights if(!booking.PickupBookingFlightID==null ||booking.f)
        }

        public BookingActionResult SendBooking(clsWebSupplierBooking booking, clsWebSupplierDespatch wsd)
        {
            return SendUpdateBooking(booking, wsd, ActionType.New);
        }

        private BookingActionResult SendUpdateBooking(clsWebSupplierBooking booking, clsWebSupplierDespatch wsd, ActionType actionType)
        {
            BookingActionResult bookingActionResult;
            LoadXml("JobRequest.xml");
            InitRequest(wsd);

            _elements.Single(x => x.Name.LocalName == "TargetSystem").Value = wsd.CordicTargetSystem;
            _elements.Single(x => x.Name.LocalName == "SourceAccount").Value = wsd.CordicSourceAccount;
            _elements.Single(x => x.Name.LocalName == "SourceJobID").Value = booking.JobNo.ToString();
          
             MapBooking(booking);
 

                        var ret = GetRequest(_soapDocument, wsd.DespatchSystemURL);

                        XElement rootElement = ret.Root;
                        var els = from elements in ret.Descendants()
                            select elements;
                        if (els.SingleOrDefault(x => x.Name.LocalName == "ErrorCode").Value == "OK")
                        {

                            var xdoc = StateRequest(booking.JobNo.ToString(), wsd);
                            var xls = from elem in xdoc.Descendants()
                                      select elem;

                            bookingActionResult = new BookingActionResult()
                            {   
                                Success = true,
                                ActionType = actionType,
                                Information = new BookingInformation()
                                {
                                    Id = xls.Single(x => x.Name.LocalName == "SourceJobID").Value,
                                    JobNumber = xls.Single(x => x.Name.LocalName == "SourceJobID").Value,
                                    Status = xls.Single(x => x.Name.LocalName == "TargetJobState").Value
                                }
                            };
                        }
                        else
                        {
                            bookingActionResult = new BookingActionResult()
                            {
                                Success = false,
                                ActionType = actionType,
                                Error = new BookingError()
                                {
                                    Code = els.FirstOrDefault(x => x.Name.LocalName == "ErrorCode").Value,
                                    Message =els.SingleOrDefault(x => x.Name.LocalName == "ErrorDetails").Value
                                }
                            };
                            
                        }

            return bookingActionResult;

        }

        private XDocument GetRequest(XDocument doc, string url)
        {
            XDocument ret=new XDocument();
            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.ContentType] = "text/xml";

                try
                {
                    byte[] data = Encoding.UTF8.GetBytes(doc.Root.ToString());

                    byte[] bytResponse = client.UploadData(url, data);

                    using (var stream = new MemoryStream(bytResponse))
                    {
                        ret = XDocument.Load(stream);
                    }

                }
                catch
                {
                }
            }
            return ret;
        }

        private XDocument StateRequest(string jobNo, clsWebSupplierDespatch wsd)
        {
           // XDocument ret = new XDocument();
            LoadXml("StateRequest.xml");
            InitRequest(wsd);
            _elements.Single(x => x.Name.LocalName == "SourceJobID").Value = jobNo;

            return GetRequest(_soapDocument, wsd.DespatchSystemURL);
        }

        public BookingActionResult Update(clsWebSupplierBooking booking, clsWebSupplierDespatch wsd)
        {
            return SendUpdateBooking(booking, wsd, ActionType.Amend);
        }
        public BookingActionResult Cancel(clsWebSupplierBooking booking, clsWebSupplierDespatch wsd)
        {
            BookingActionResult bookingActionResult;
            // XDocument ret = new XDocument();
            LoadXml("CancelRequest.xml");
            InitRequest(wsd);
            _elements.Single(x => x.Name.LocalName == "SourceJobID").Value = booking.DespatchSystemBookingID;
                        var ret = GetRequest(_soapDocument, wsd.DespatchSystemURL);

                        XElement rootElement = ret.Root;
                        var els = from elements in ret.Descendants()
                            select elements;
                        if (els.Single(x => x.Name.LocalName == "ErrorCode").Value == "OK")
                        {
                            var xdoc = StateRequest(booking.JobNo.ToString(), wsd);
                            var xls = from elements in xdoc.Descendants()
                                  select elements;

                            bookingActionResult = new BookingActionResult()
                            {
                                Success = true,
                                ActionType = ActionType.Cancel,
                                Information = new BookingInformation()
                                {
                                    Id = xls.Single(x => x.Name.LocalName == "TargetJobID").Value,
                                    JobNumber = xls.Single(x => x.Name.LocalName == "TargetJobID").Value,
                                    Status = xls.Single(x => x.Name.LocalName == "TargetJobState").Value
                                }
                            };
                        }
                        else
                        {
                            bookingActionResult = new BookingActionResult()
                            {
                                Success = false,
                                ActionType = ActionType.Cancel,
                                Error = new BookingError()
                                {
                                    Code = els.Single(x => x.Name.LocalName == "ErrorCode").Value,
                                    Message = els.Single(x => x.Name.LocalName == "ErrorDetails").Value
                                }
                            };

                        }

           return   bookingActionResult;
        }


        private string ProcessDriverNotes(clsWebSupplierBooking wsb)
        {
            string driverNotes = "";
            if (wsb.WaitAndReturn == "True")
            {
                driverNotes += ", Wait and Return";
            }
            if (wsb.MeetAndGreet == "True")
            {
                driverNotes += ", Meet and Greet";
            }
            if (wsb.CallOnArrival == "True")
            {
                driverNotes += ", Call on Arrival";
            }
           // if (driverNotes.Length > 0)
          //  {
          //      driverNotes += ", ";
          //  }

            if (wsb.SpecialInstructions != "")
            {
                driverNotes += ", " + wsb.SpecialInstructions;
            }
            if (driverNotes.StartsWith(","))
            {
                driverNotes = driverNotes.Substring(1, driverNotes.Length - 1);
            }
            return driverNotes.Trim();

        }
 

    }
}
