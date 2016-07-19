using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using Booking;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;


namespace Cabfind.DespatchManager.Despatchers
{
    public class AutoCab : IDespatchStrategy
    {
        private XDocument _document;
        private IEnumerable<System.Xml.Linq.XElement> _elements;
        private XElement _rootElement;
        private string _xmlFolder = System.Text.RegularExpressions.Regex.Split(AppDomain.CurrentDomain.BaseDirectory.Replace("/", "\\"), @"bin")[0] + "AutoCabXML\\";
        private HttpClient _client;
        private Helper _helper;

        private Func<Booking.clsWebSupplierBooking, string> Facilities = (b) =>
        {
            if (b.WheelChair == "True" | b.AutoCabSupplierVehicle.ToString().Trim() == "Wheelchair")
            {
                return "Wheelchair";
            }
            return "None";
        };
        
        public AutoCab()
        {
            _helper = new Helper();
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            _client = new HttpClient(new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Automatic
            });
        }


        public BookingActionResult SendBooking(Booking.clsWebSupplierBooking booking, Booking.clsWebSupplierDespatch wsd)
        {
            LoadXml("AgentBookingAvailabilityRequest.xml");
            InitRequest(wsd);
            MapBooking(booking);

            return Send(booking, wsd, ActionType.New);

        }

        private BookingActionResult Send(Booking.clsWebSupplierBooking booking, Booking.clsWebSupplierDespatch wsd, ActionType actionType)
        {
            BookingActionResult bookingActionResult = null;
            HttpContent content = new StringContent(_rootElement.ToString(), Encoding.UTF8, "text/xml");
            content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
            var response = _client.PostAsync(new Uri(wsd.DespatchSystemURL), content).Result;
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(response.Content.ReadAsStringAsync().Result);
            var json = JsonConvert.SerializeXmlNode(doc, Formatting.None, true);
            var jsonResponse = JObject.Parse(json);

            if (jsonResponse.GetValue("Result").SelectToken("Success").Value<bool>())
            {
                LoadXml("AgentBookingAuthorizationRequest.xml");
                InitRequest(wsd);
                _elements.Single(x => x.Name.LocalName == "AvailabilityReference").Value =
                    jsonResponse.GetValue("AvailabilityReference").Value<string>();

                _elements.ElementAt(9).SetValue(booking.ClientLeadName);
                _elements.ElementAt(10).SetValue(booking.PassengerTelNo ?? "");
                _elements.ElementAt(11).SetValue(booking.SpecialInstructions ?? "");
                if (booking.CallOnArrival != "False")
                {
                    _elements.ElementAt(14).SetValue("Ringback");
                }


                content = new StringContent(_rootElement.ToString(), Encoding.UTF8, "text/xml");
                response = _client.PostAsync(new Uri(wsd.DespatchSystemURL), content).Result;
                doc.LoadXml(response.Content.ReadAsStringAsync().Result);
                json = JsonConvert.SerializeXmlNode(doc, Formatting.None, true);
                jsonResponse = JObject.Parse(json);
                if (jsonResponse.GetValue("Result").SelectToken("Success").Value<bool>())
                {
                    bookingActionResult = new BookingActionResult()
                    {
                        ActionType = actionType,
                        Success = true,
                        Information = new BookingInformation()
                        {
                            Id = jsonResponse.GetValue("AuthorizationReference").ToString(),
                            JobNumber = booking.JobNo.ToString(),
                            Status = "BookedActive"
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
                            Code = jsonResponse.GetValue("Result").SelectToken("FailureCode").Value<string>(),
                            Message = jsonResponse.GetValue("Result").SelectToken("FailureReason").Value<string>()
                        }
                    };
                }
            }
            else
            {
                bookingActionResult= new BookingActionResult()
                {
                    Success = false,
                    ActionType = actionType,
                    Error = new BookingError()
                    {
                        Code = jsonResponse.GetValue("Result").SelectToken("FailureCode").Value<string>(),
                        Message = jsonResponse.GetValue("Result").SelectToken("FailureReason").Value<string>()
                    }
                };
            }

            return bookingActionResult;
        }
        public BookingActionResult Update(Booking.clsWebSupplierBooking booking, Booking.clsWebSupplierDespatch wsd)
        {
            BookingActionResult bookingActionResult;
            bookingActionResult = Cancel(booking, wsd, ActionType.Cancel);

            if (bookingActionResult.Success == true)
            {
                LoadXml("AgentBookingAvailabilityRequest.xml");
                InitRequest(wsd);
                MapBooking(booking);
                bookingActionResult= Send(booking, wsd, ActionType.Amend);
            }
            return bookingActionResult;

            //  return bookingActionResult.Success == true
            //      ? Send(booking, wsd, ActionType.Amend)
            //      : bookingActionResult;
        }

        public BookingActionResult Cancel(Booking.clsWebSupplierBooking booking, Booking.clsWebSupplierDespatch wsd)
        {
            return Cancel(booking, wsd, ActionType.Cancel);
        }

        private BookingActionResult Cancel(Booking.clsWebSupplierBooking booking, Booking.clsWebSupplierDespatch wsd, ActionType actionType)
        {
            LoadXml("AgentBookingCancellationRequest.xml");
            InitRequest(wsd);
            BookingActionResult bookingActionResult = null;
            _elements.Single(x => x.Name.LocalName == "AuthorizationReference").Value = booking.DespatchSystemBookingID.Trim();
            HttpContent content = new StringContent(_rootElement.ToString(), Encoding.UTF8, "text/xml");
            content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
            var response = _client.PostAsync(new Uri(wsd.DespatchSystemURL), content).Result;
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(response.Content.ReadAsStringAsync().Result);
            var json = JsonConvert.SerializeXmlNode(doc, Formatting.None, true);
            var jsonResponse = JObject.Parse(json);
            if (jsonResponse.GetValue("Result").SelectToken("Success").Value<bool>())
            {
                bookingActionResult = new BookingActionResult()
                {
                    ActionType = actionType,
                    Success = true,
                    Information = new BookingInformation()
                    {
                        Id = jsonResponse.GetValue("Agent").SelectToken("Reference").Value<string>(),
                        JobNumber = booking.JobNo.ToString(),
                        Status = "Cancelled"
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
                        Code = jsonResponse.GetValue("Result").SelectToken("FailureCode").Value<string>(),
                        Message = jsonResponse.GetValue("Result").SelectToken("FailureReason").Value<string>()
                    }
                };
            }
            return bookingActionResult;
        }
        private void MapBooking(Booking.clsWebSupplierBooking booking)
        {
            //***Pickup Date Time
            /*
            if (booking.PickupDateTime.ToUniversalTime() > DateTime.UtcNow.AddMinutes(10.00))
            {
                _elements.Single(x => x.Name.LocalName == "BookingTime").Value =  booking.PickupDateTime.ToUniversalTime().ToString("s");
            }
             * */
            _elements.Single(x => x.Name.LocalName == "BookingTime").Value = booking.PickupDateTime.ToString("s");



            _elements.ElementAt(14).SetValue(booking.PickupAddressOneLine);
            string[] ret = new string[] { "0", "0" };
            //ret= GoogleGeocode.getLatLng(booking.PickupAddressOneLine);
            ret = _helper.PostCodeToLatLng(booking.PickupAddressPostcodeFirstPart,
                booking.PickupAddressPostcodeSecondPart);
            _elements.ElementAt(16).SetValue(ret[0]);
            _elements.ElementAt(17).SetValue(ret[1]);
            _elements.ElementAt(20).SetValue(booking.DestAddressOneLine);
            ret = new string[] { "0", "0" };
            // ret = GoogleGeocode.getLatLng(booking.DestAddressOneLine);
            ret = _helper.PostCodeToLatLng(booking.DestAddressPostcodeFirstPart, booking.DestAddressPostcodeSecondPart);
            _elements.ElementAt(22).SetValue(ret[0]);
            _elements.ElementAt(23).SetValue(ret[1]);
            _elements.ElementAt(25).SetValue(booking.ClientPassengers.ToString());
            _elements.ElementAt(26).SetValue(Facilities(booking));
            //
            // AutoCab does not have vehicle type Wheelchair. It manages it through facility
            if (booking.VehicleTypeDescription == "Wheelchair" | booking.AutoCabSupplierVehicle.Trim() == "Wheelchair")
            {
                _elements.ElementAt(26).SetValue("Wheelchair");
                _elements.ElementAt(28).SetValue("Saloon");
            }
            else
            {
                _elements.ElementAt(28).SetValue(booking.AutoCabSupplierVehicle.Trim());
            }


            
        }

        private void LoadXml(string xmlFile)
        {
            string filepath = _xmlFolder + xmlFile;
            _document = XDocument.Load(filepath);
            // rootNamespace = _document.Root.Name.NamespaceName;
        }

        private void InitRequest(clsWebSupplierDespatch wsd)
        {
            _rootElement = _document.Root;
            _elements = from elements in _rootElement.Descendants()
                        select elements;
            _elements.Single(x => x.Name.LocalName == "Password").Value = wsd.AutoCabPassword;
            _elements.Single(x => x.Name.LocalName == "Agent").FirstAttribute.Value = wsd.AutoAgent;
            _elements.Single(x => x.Name.LocalName == "Vendor").FirstAttribute.Value = wsd.AutoCabVendor;
            _elements.Single(x => x.Name.LocalName == "Time").Value =  DateTime.Now.ToString("s");

        }
    }
}
