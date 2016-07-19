using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Booking;
using Newtonsoft.Json.Linq;
using Cabfind.DespatchManager;

namespace Cabfind.DespatchManager.Despatchers
{
    public class AddisonLee : IDespatchStrategy
    {
        private HttpClient _client;
        private Helper _helper = new Helper();
        private Uri _baseAddress;

        /// <summary>
        /// Format Ids and errors (multiple for future use)
        /// </summary>
        private Func<Task<Cabfind.DespatchManager.BookingActionResult>[], string[]> Format = delegate(Task<Cabfind.DespatchManager.BookingActionResult>[] bookingResults)
        {
            var idList = new List<Task<Cabfind.DespatchManager.BookingActionResult>>(bookingResults.Length);
            idList.AddRange(bookingResults);
            string[] ret = new string[2];
            idList.ForEach(x =>
            {
                if (x.Result.Success == true)
                {
                    ret[0] = ret[0] + x.Result.Information.Id + ",";
                }
                else
                {
                    ret[1] = ret[1] + x.Result.Error.Code + "-" + x.Result.Error.Message;
                }

            });
            if (ret[0] != null)
                ret[0] = ret[0].TrimEnd(',');
            if (ret[1] != null)
                ret[1] = ret[1].TrimEnd(',');

            return ret;

        };

        public AddisonLee()
        {
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            _client =
                HttpClientFactory.Create(
                    new HttpClientHandler()
                    {
                        ClientCertificateOptions = ClientCertificateOption.Automatic,
                        CookieContainer = new CookieContainer()
                    });
        }

        public BookingActionResult Update(clsWebSupplierBooking booking, clsWebSupplierDespatch wsd)
        {
            _baseAddress = new Uri(wsd.DespatchSystemURL);
            OpenSession(wsd);
            Authorize(wsd);
            string[] ids = booking.DespatchSystemBookingID.Split(',');

            int count = booking.NoOfVehicles;
            var bookingActionResult = new BookingActionResult() { ActionType = ActionType.Amend };
            if (ids.Count() == booking.NoOfVehicles)
            {
                var tasks = new Task<BookingActionResult>[count];
                for (var i = 0; i < count; i++)
                {
                    Task<BookingActionResult> t = this.SendUpdate(booking, wsd, ActionType.Amend, ids[i]);
                    tasks.SetValue(t, i);
                }

                Task.Factory.ContinueWhenAll(tasks, (ret) =>
                {
                    var f = ret.ToList<Task<BookingActionResult>>().FirstOrDefault(x => x.Result.Success == false);
                    var format = Format(ret);
                    if (f == null)
                    {
                        bookingActionResult.Information = new BookingInformation()
                        {
                            Id = format[0],
                            JobNumber = format[0], // ret[0].Result.Information.JobNumber,
                            Status = ret[0].Result.Information.Status
                        };
                        bookingActionResult.Success = ret[0].Result.Success;
                    }
                    else
                    {
                        bookingActionResult.Success = false;
                        bookingActionResult.Error = new BookingError() { Code = f.Result.Error.Code, Message = format[1] };
                    }
                }).Wait();
            }
            else
            {
                var tasks = new Task<BookingActionResult>[ids.Count()];
                for (var i = 0; i < ids.Count(); i++)
                {
                    Task<BookingActionResult> t = this.CancelBooking(ids[i], booking.JobNo.ToString(), wsd);
                    tasks.SetValue(t, i);
                }
                Task.Factory.ContinueWhenAll(tasks, (retCancel) =>
                {
                    var f = retCancel.ToList<Task<BookingActionResult>>().FirstOrDefault(x => x.Result.Success == false);
                    if (f == null)
                    {
                        tasks = new Task<BookingActionResult>[booking.NoOfVehicles];
                        for (var i = 0; i < booking.NoOfVehicles; i++)
                        {
                            Task<BookingActionResult> t = this.SendUpdate(booking, wsd, ActionType.New);
                            tasks.SetValue(t, i);
                        }
                        Task.Factory.ContinueWhenAll(tasks, (retNew) =>
                        {
                            f = retNew.ToList<Task<BookingActionResult>>().FirstOrDefault(x => x.Result.Success == false);
                            var format = Format(retNew);
                            if (f == null)
                            {
                                bookingActionResult.Information = new BookingInformation()
                                {
                                    Id = format[0],
                                    JobNumber = format[0], //retNew[0].Result.Information.JobNumber,
                                    Status = retNew[0].Result.Information.Status
                                };
                                bookingActionResult.Success = retNew[0].Result.Success;
                            }
                            else
                            {
                                bookingActionResult.Error = new BookingError() { Code = f.Result.Error.Code, Message = format[1] };
                                bookingActionResult.Success = false;
                            }
                        }).Wait();
                    }
                }).Wait();
            }
            return bookingActionResult;
        }

        public BookingActionResult SendBooking(Booking.clsWebSupplierBooking booking, Booking.clsWebSupplierDespatch wsd)
        {
            _baseAddress = new Uri(wsd.DespatchSystemURL);
            OpenSession(wsd);
            Authorize(wsd);
            string[] ids = booking.DespatchSystemBookingID.Split(',');

            int count = booking.NoOfVehicles;
            var bookingActionResult = new BookingActionResult() { ActionType = ActionType.New };
            var tasks = new Task<BookingActionResult>[count];
            for (var i = 0; i < count; i++)
            {
                Task<BookingActionResult> t = this.SendUpdate(booking, wsd, ActionType.New, ids[i]);
                tasks.SetValue(t, i);
            }

            Task.Factory.ContinueWhenAll(tasks, (ret) =>
            {
                var f = ret.ToList<Task<BookingActionResult>>().FirstOrDefault(x => x.Result.Success == false);
                var format = Format(ret);
                if (f == null)
                {
                    bookingActionResult.Information = new BookingInformation()
                    {
                        Id = format[0],
                        JobNumber = format[0], // ret[0].Result.Information.JobNumber,
                        Status = ret[0].Result.Information.Status
                    };
                    bookingActionResult.Success = ret[0].Result.Success;
                }
                else
                {
                    bookingActionResult.Success = false;
                    bookingActionResult.Error = new BookingError() { Code = f.Result.Error.Code, Message = format[1] };
                }
            }).Wait();
            return bookingActionResult;
        }

        private async Task<BookingActionResult> SendUpdate(Booking.clsWebSupplierBooking booking, Booking.clsWebSupplierDespatch wsd, ActionType actionType, string id = "")
        {
            BookingActionResult bookingActionResult = null;
            var pickUplatlng = _helper.PostCodeToLatLng(booking.PickupAddressPostcodeFirstPart,
                                                        booking.PickupAddressPostcodeSecondPart);
            var destUplatlng = _helper.PostCodeToLatLng(booking.DestAddressPostcodeFirstPart,
                                                        booking.DestAddressPostcodeSecondPart);

            string[] addressFormat = booking.GetAddisonLeeformatAddress();

            string notes = string.IsNullOrEmpty(booking.SpecialInstructions) == true ? "" : booking.SpecialInstructions;
            if (booking.CallOnArrival == "True")
                notes += " Call On Arrival";
            if (booking.WaitAndReturn == "True")
                notes += " WaitAndReturn";
            if (booking.MeetAndGreet == "True")
                notes += " Meet And Greet";
            if (booking.WheelChair == "True")
                notes += " WheelChair";

            var instructions = string.IsNullOrEmpty(notes) == true ? "" : "\"instructions\" : [ { \"type\" : \"Notes\"," + "\"value\" : \"" + notes + "\"" + " }]" + ",";

            if (booking.PickupBookingFlightID > 0)
            {
                var airports = "{" +
                               "\"method\" : \"getSpecialPlaces\"," +
                               "\"id\" : \"790410093961073447\"," +
                               "\"params\": [[ " + "\"Airports\" ]]," +
                               "\"jsonRpc\" : \"2.0\"" + "}";
                HttpContent content = new StringContent(airports, Encoding.UTF8, "application/json");
                var authorizeResponse = _client.PostAsync(wsd.DespatchSystemURL, content).Result;
                JObject jsonResult = JObject.Parse(authorizeResponse.Content.ReadAsStringAsync().Result);
                var addresses = ((dynamic) jsonResult).result.addresses as JArray;
                addresses.ToList()
                    .Select(x => x.SelectToken("address").Value<string>() == "Heathrow, Term 1 (international)")
                    .ToList();

                var item = addresses.Where(x =>

                    x["address"].ToString() == "Heathrow, Term 1 (international)"

                    ).FirstOrDefault();


                var str = ((JObject) item).ToString(Newtonsoft.Json.Formatting.None);

            }
           


            var strPickup = "{" +
               "\"@class\" : \"Stop\"," +
               "\"address\" : {" +
               "\"address\" : \"" + addressFormat[1] + "\"" + "," +
               "\"name\" :\"" + addressFormat[1] + ", " + pickUplatlng[2] + ", " + booking.PickupAddressPostcodeFirstPart + " " + booking.PickupAddressPostcodeSecondPart + "\"" + "," +
                // "\"source\" : \"QAS\"," +
               "\"latitude\" :\"" + pickUplatlng[0] + "\"" + "," +
               "\"longitude\" :\"" + pickUplatlng[1] + "\"" + "," +
               "\"town\" : \"" + pickUplatlng[2] + "\"" + "," +
               "\"postcode\" : \"" + booking.PickupAddressPostcodeFirstPart + " " + booking.PickupAddressPostcodeSecondPart + "\"" + //"," +
                //  "\"accuracy\" : \"M1\"," +
                //   "\"streetName\" : \"" + booking.PickupAddressOneLine + "\"" + "," +
                //  "\"hasBeneath\" : false," +
                //  "\"relevance\" : 96," +
                //  "\"refined\" : true" +
               "}" +
               "}";
            var strDropOff = "{" +
                "\"@class\" : \"Stop\"," +
                "\"address\" : {" +
                "\"address\" : \"" + addressFormat[3] + "\"" + "," +
                "\"name\" :\"" + addressFormat[3] + ", " + destUplatlng[2] + ", " + booking.DestAddressPostcodeFirstPart + " " + booking.DestAddressPostcodeSecondPart + "\"" + "," +
                // "\"source\" : \"QAS\"," +
                "\"latitude\" :\"" + destUplatlng[0] + "\"" + "," +
                "\"longitude\" :\"" + destUplatlng[1] + "\"" + "," +
                "\"town\" : \"" + destUplatlng[2] + "\"" + "," +
                "\"postcode\" : \"" + booking.DestAddressPostcodeFirstPart + " " + booking.DestAddressPostcodeSecondPart + "\"" + //"," +
                //  "\"accuracy\" : \"M1\"," +
                //    "\"streetName\" : \"" + booking.DestAddressOneLine + "\"" + "," +
                //    "\"hasBeneath\" : false," +
                //    "\"relevance\" : 96," +
                //    "\"refined\" : true" +
                "}" +
                "}";

            string strVias = "";
            if (booking.BookingVias.Vias.Count > 0)
            {
                int i = 0;
                DataTable tblVias = booking.GetAddisonLeeformatViaAddress();
                booking.BookingVias.Vias.ForEach(x =>
                {

                    var vialatlng = _helper.PostCodeToLatLng(x.Address.OutCode,
                                                        x.Address.InCode);
                    strVias = strVias + "{" +
                              "\"@class\" : \"Stop\"," +
                              "\"address\" : {" +
                              "\"address\" : \"" + tblVias.Rows[i][0].ToString() + "\"" + "," +
                              "\"name\" :\"" + tblVias.Rows[i][0].ToString() + vialatlng[2] + ", " + x.Address.OutCode + " " +
                              x.Address.InCode + "\"" + "," +
                        // "\"source\" : \"QAS\"," +
                              "\"latitude\" :\"" + vialatlng[0] + "\"" + "," +
                              "\"longitude\" :\"" + vialatlng[1] + "\"" + "," +
                              "\"town\" : \"" + vialatlng[2] + "\"" + "," +
                              "\"postcode\" : \"" + x.Address.OutCode + " " +
                              x.Address.InCode + "\"" + //"," +
                        //  "\"accuracy\" : \"M1\"," +
                        //      "\"streetName\" : \"" + x.Address.FullDescription + "\"" + //"," +
                        //       "\"hasBeneath\" : false," +
                        //       "\"relevance\" : 96," +
                        //        "\"refined\" : true" +
                              "}" +
                              "} , ";
                    i++;
                });
            }

            DateTime pickdate = Convert.ToDateTime(booking.PickupDateTime);
            string asap = pickdate > DateTime.Now.AddMinutes(10.00) ? "false" : "true";

            var strPickdate = pickdate.Year.ToString() + "-" + pickdate.Month.ToString() + "-" + pickdate.Day.ToString() +
                              " " + pickdate.TimeOfDay.ToString();
            //var service = "OneFourPassengers";
            string param = actionType == ActionType.New ? "\"params\" : [ \"" + Guid.NewGuid().ToString() + "\"" + ", {"
                : "\"params\" : [ {" +
                "\"id\" : " + "\"" + id + "\"" + ",";
            string commitAmendJob = actionType == ActionType.New ? "commitJob" : "amendJob";
            var commitJob = "{" +
               "\"method\" : " + "\"" + commitAmendJob + "\"" + "," +
               "\"id\" : " + "\"" + DateTime.Now.ToFileTime().ToString() + "\"" + "," +
               param +
                /*
                "\"params\" : [ \"" + Guid.NewGuid().ToString() + "\"" + ", {" +                           
                "\"params\" : [ {" +
                "\"id\" : " + "\"" + jobRequest.Job.JobNumber + "\"" + "," +
                */
               "\"date\" : \"" + strPickdate + "\"" + "," +
                //"\"asap\" : false ," +
                "\"asap\" : " + asap + "," +
               "\"service\" : \"" + booking.AddisonLeeSupplierVehicle.Trim() + "\"" + "," +
               "\"references\" : {" +
               "\"PO NO\" : \"" + booking.JobNo + "\"" +
               "}," +
               "\"paymentMethod\" : {" +
               "\"type\" : \"Account\"" + "," +
               "\"details\" : {" +
               "\"paymentMethod\" : {" +
               "\"type\" : \"Account\"" +
               "}," +
               "\"token\" : \"" + booking.AccountNoToken.Trim() + "\"" +    // "7f9cfdb7-1c87-4af1-99a4-df843eb102eb" + "\"" + //Yes hard coded
                "}}," + instructions +
                // "\"instructions\" : [ { \"type\" : \"Notes\"," +
                // "\"value\" : \"" + notes + "\"" +
                // " }]" + "," +
               "\"passenger\" : {" +
               "\"name\"  : \"" + booking.ClientLeadName + "\"" + "," +
               "\"telephone\"  : \"" + booking.PassengerTelNo + "\"" + "," +
                "\"email\"  : \"" + "" + "\"" + "}," +
                "\"stops\" : [ " +
               strPickup + "," + strVias + strDropOff +
               " ]" +
               "} ]," +
               "\"jsonRpc\" : \"2.0\"}";
            HttpContent commitJobRequestContent = new StringContent(commitJob, Encoding.UTF8, "application/json");
            try
            {
                var commitJobResponse = _client.PostAsync(_baseAddress, commitJobRequestContent).Result;

                var jObj = JObject.Parse(commitJobResponse.Content.ReadAsStringAsync().Result);
                //We have error
                if (jObj.SelectToken("$.error") != null)
                {
                    bookingActionResult = new BookingActionResult()
                    {
                        ActionType = actionType,
                        Error = new BookingError() { Code = jObj.SelectToken("$..code").ToString(), Message =   jObj.SelectToken("$..message").ToString() },
                        Success = false
                    };
                }
                else
                {
                    var ret = jObj.GetValue("result").SelectToken("id").Value<string>();
                    var number = jObj.GetValue("result").SelectToken("number").Value<string>();
                    bookingActionResult = new BookingActionResult()
                    {
                        ActionType = actionType,
                        Information = new BookingInformation() { Id = ret, JobNumber = ret, Status = "Booked" },
                        Success = true
                    };
                }
            }
            catch (Exception ex)
            {
                bookingActionResult = new BookingActionResult()
                {
                    ActionType = actionType,
                    Error = new BookingError() { Code = ex.Source, Message = ex.Message },
                    Success = false
                };
            }
            return bookingActionResult;
        }


        private void OpenSession(clsWebSupplierDespatch wsd)
        {
            var openSessionBody = "{" +
             "\"method\": \"openSession\"," +
             "\"id\" : " + "\"" + DateTime.Now.ToFileTime().ToString() + "\"" + "," +
             "\"params\": [ {" +
             "\"appVersion\": 1," +
             "\"appId\": " + "\"" + wsd.AddisonLeeAppId + "\"" + "," +
             "\"deviceId\": \"AL Dispatcher\"," +
             "\"fingerprint\": " + "\"" + wsd.AddisonLeeAppId + "\"" +
             "} ]," +
             "\"jsonRpc\": \"2.0\"" +
             "}";

            HttpContent content = new StringContent(openSessionBody, Encoding.UTF8, "application/json");

            var openSessionResponse = _client.PostAsync(wsd.DespatchSystemURL, content).Result;
            JObject jSonResult = JObject.Parse(openSessionResponse.Content.ReadAsStringAsync().Result);
            var jsId = ((dynamic)jSonResult).result.JSESSIONID.Value as string;
            _baseAddress = new Uri(_baseAddress.ToString() + ";jsessionid=" + jsId);
            wsd.DespatchSystemURL = _baseAddress.ToString();

        }

        private void Authorize(clsWebSupplierDespatch wsd)
        {
            var authorize = "{" +
               "\"method\": \"authToken\"," +
               "\"id\": " + "\"" + DateTime.Now.ToFileTime().ToString() + "\"" + "," +
               "\"params\": [ \"" + wsd.AddisonLeeToken + "\"" +
               "]," +
               "\"jsonRpc\": \"2.0\"" +
               "}";
            HttpContent content = new StringContent(authorize, Encoding.UTF8, "application/json");
            var authorizeResponse = _client.PostAsync(wsd.DespatchSystemURL, content).Result;
            JObject jSonResult = JObject.Parse(authorizeResponse.Content.ReadAsStringAsync().Result);
        }


        public BookingActionResult Cancel(clsWebSupplierBooking booking, clsWebSupplierDespatch wsd)
        {
            var bookingActionResult = new BookingActionResult() { ActionType = ActionType.Cancel };
            string[] ids = booking.DespatchSystemBookingID.Split(',');
            var count = ids.Count();
            var tasks = new Task<BookingActionResult>[count];
            for (var i = 0; i < count; i++)
            {
                Task<BookingActionResult> t = this.CancelBooking(ids[i], booking.JobNo.ToString(CultureInfo.InvariantCulture), wsd);
                tasks.SetValue(t, i);
            }

            Task.Factory.ContinueWhenAll(tasks, (ret) =>
            {
                var t = ret.ToList<Task<BookingActionResult>>().FirstOrDefault(x => x.Result.Success == false);
                if (t == null)
                {
                    bookingActionResult.Information = new BookingInformation()
                    {
                        Id = booking.DespatchSystemBookingID,
                        JobNumber = booking.DespatchSystemBookingID,
                        Status = ret[0].Result.Information.Status
                    };
                    bookingActionResult.Success = ret[0].Result.Success;
                }
                else
                {
                    bookingActionResult.Error = new BookingError() { Code = t.Result.Error.Code, Message = t.Result.Error.Message };
                    bookingActionResult.Success = false;
                }

            }).Wait();
            return bookingActionResult;
        }

        private async Task<BookingActionResult> CancelBooking(string perma_id, string jobNumber,
            Booking.clsWebSupplierDespatch wsd)
        {
            _baseAddress = new Uri(wsd.DespatchSystemURL);
            OpenSession(wsd);
            Authorize(wsd);
            BookingActionResult bookingActionResult = null;

            var cancelJob = "{" +
                          "\"method\" : \"cancelJob\"," +
                          "\"id\": " + "\"" + DateTime.Now.ToFileTime().ToString() + "\"" + "," +
                          "\"params\" : [ \"" + perma_id + "\"" + " ]," +
                          "\"jsonRpc\" : \"2.0\"" +
                          "}";

            HttpContent content = new StringContent(cancelJob, Encoding.UTF8, "application/json");
            try
            {
                var cancelJobResponse = _client.PostAsync(_baseAddress, content).Result;

                var jObj = JObject.Parse(cancelJobResponse.Content.ReadAsStringAsync().Result);
                //We have error
                if (jObj.SelectToken("$.error") != null)
                {
                    bookingActionResult = new BookingActionResult()
                    {
                        ActionType = ActionType.Cancel,
                        Error = new BookingError() { Code = jObj.SelectToken("$..code").ToString(), Message = jObj.SelectToken("$..message").ToString() },
                        Success = false
                    };
                }
                else
                {
                    var ret = (string)jObj["ID"];
                    bookingActionResult = new BookingActionResult()
                    {
                        ActionType = ActionType.Cancel,
                        Information = new BookingInformation() { Id = ret, JobNumber = ret, Status = "Cancelled" },
                        Success = true
                    };
                }
            }
            catch (Exception ex)
            {
                bookingActionResult = new BookingActionResult()
                {
                    ActionType = ActionType.Cancel,
                    Error = new BookingError() { Code = ex.Source, Message = ex.Message },
                    Success = false
                };
            }
            return bookingActionResult;

        }
    }
}
