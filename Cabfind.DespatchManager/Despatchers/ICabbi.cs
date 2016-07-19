using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Booking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;


namespace Cabfind.DespatchManager.Despatchers
{
    public class ICabbi : IDespatchStrategy
    {
        private static  HttpClient _client;
        private Helper _helper = new Helper();

        private delegate Task<BookingActionResult> ActionMethod(
            Booking.clsWebSupplierBooking booking, Booking.clsWebSupplierDespatch wsd);


        /// <summary>
        /// Format Ids and errors (multiple for future use)
        /// </summary>
        private Func<Task<Cabfind.DespatchManager.BookingActionResult>[], string[]> Format = delegate(Task<Cabfind.DespatchManager.BookingActionResult>[] bookingResults)
        {
            var idList = new List<Task<Cabfind.DespatchManager.BookingActionResult>>(bookingResults.Length);
            idList.AddRange(bookingResults);
            string[] ret =new string[2];
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
            if (ret[0]!=null)
                ret[0]=ret[0].TrimEnd(',');
            if (ret[1] != null)
                ret[1]=ret[1].TrimEnd(',');
            
            return ret;
        };

        public ICabbi()
        {
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            _client = new HttpClient(new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Automatic
            });
        }

        private Task<BookingActionResult>[] CreateTasks(int count, ActionType actionType, Task<BookingActionResult> task) 
        {
            var bookingActionResult = new BookingActionResult() { ActionType = actionType};
            var tasks = new Task<BookingActionResult>[count];
            for (var i = 0; i < count; i++)
            {
                Task<BookingActionResult> t = task;
                tasks.SetValue(t, i);
            }
            return tasks;
        }


        public BookingActionResult SendBooking(Booking.clsWebSupplierBooking booking, Booking.clsWebSupplierDespatch wsd)
        {
           // var bookingActionResult = new BookingActionResult() { ActionType = ActionType.New };
          //  var t = CreateTasks(booking.NoOfVehicles, ActionType.New, this.Send(booking, wsd));
           
            int count = booking.NoOfVehicles;
            var bookingActionResult = new BookingActionResult() {ActionType = ActionType.New};
            var tasks = new Task<BookingActionResult>[count];
            for (var i = 0; i < count; i++)
            {
                Task<BookingActionResult> t = this.SendUpdate(booking, wsd, ActionType.New);
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
                        JobNumber =format[0], // ret[0].Result.Information.JobNumber,
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

        private async Task<BookingActionResult>  SendUpdate(Booking.clsWebSupplierBooking booking, Booking.clsWebSupplierDespatch wsd, ActionType actionType, string perma_id="")
        {
            BookingActionResult bookingActionResult = null;
            iCabbiBooking book = MapBooking(booking);
            book.perma_id = perma_id;
            book.id = perma_id;
            book.account_id = wsd.iCabbiAccountId;
            book.user_id = wsd.iCabbiUserId;
            var requestMessage = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = actionType==ActionType.New ? new Uri(wsd.DespatchSystemURL + "bookings/add?app_key=" + wsd.iCabbiApiKey + "&phone=%2B" + wsd.iCabbiPhoneUser)
                                                        : new Uri(wsd.DespatchSystemURL + "bookings/update/" + perma_id + "?app_key=" + wsd.iCabbiApiKey + "&phone=%2B" + wsd.iCabbiPhoneUser),
                Content =  new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("booking", JsonConvert.SerializeObject(book)),
                    new KeyValuePair<string, string>("secretkey", wsd.iCabbiSecretKey)
                })
            };
            try
            {
                var response = await  _client.SendAsync(requestMessage);
                var jObj = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                //We have error
                if (jObj.SelectToken("$.error") != null)
                {
                    bookingActionResult= new BookingActionResult()
                    {
                        ActionType = actionType,
                        Error = new BookingError() { Code = jObj.SelectToken("$.code").ToString(), Message = jObj.SelectToken("$.message").ToString() },
                        Success = false
                    };
                }
                else
                {
                    var ret=  jObj.SelectToken("$..booking").ToObject<iCabbiBooking>();
                    bookingActionResult = new BookingActionResult()
                    {
                        ActionType = actionType,
                        Information = new BookingInformation() { Id = ret.perma_id, JobNumber = ret.perma_id, Status = ret.status },
                        Success = true
                    };
                }
            }
            catch (Exception ex)
            {
                bookingActionResult = new BookingActionResult()
                {
                    ActionType = actionType,
                    Error = new BookingError() {Code = ex.Source, Message = ex.Message},
                    Success = false
                };
            }
            return bookingActionResult;
        }

        public BookingActionResult Update(Booking.clsWebSupplierBooking booking, Booking.clsWebSupplierDespatch wsd)
        {
            var bookingActionResult = new BookingActionResult() {ActionType = ActionType.Amend};
            //Task<BookingActionResult>[] tasks;
            string[] ids = booking.DespatchSystemBookingID.Split(',');
            if (ids.Count() == booking.NoOfVehicles)
            {
                var tasks = new Task<BookingActionResult>[booking.NoOfVehicles];
                for (var i = 0; i < booking.NoOfVehicles; i++)
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
                            JobNumber =format[0], // ret[0].Result.Information.JobNumber,
                            Status = ret[0].Result.Information.Status
                        };
                        bookingActionResult.Success = ret[0].Result.Success;
                    }
                    else
                    {
                        bookingActionResult.Error = new BookingError() { Code = f.Result.Error.Code, Message = format[1] };
                        bookingActionResult.Success = false;
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

        public BookingActionResult Cancel(Booking.clsWebSupplierBooking booking, Booking.clsWebSupplierDespatch wsd)
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
                if (t== null)
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

        private async Task<BookingActionResult> CancelBooking(string perma_id, string  jobNumber, Booking.clsWebSupplierDespatch wsd)
        {
            BookingActionResult bookingActionResult = null;
            var requestMessage = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(wsd.DespatchSystemURL + "bookings/cancel/" + perma_id ), 
                Content = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
                {  
                    new KeyValuePair<string, string>("secretkey", wsd.iCabbiSecretKey),
                    new KeyValuePair<string, string>("app_key", wsd.iCabbiApiKey)
                })
            };
            try
            {
                var response = await _client.SendAsync(requestMessage);
                var jObj = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                if (jObj.SelectToken("$.error") != null)
                {
                    bookingActionResult = new BookingActionResult()
                    {
                        ActionType = ActionType.Cancel,
                        Error = new BookingError() { Code = jObj.SelectToken("$.code").ToString(), Message = jObj.SelectToken("$.message").ToString() },
                        Success = false
                    };
                }
                else
                {
                    bookingActionResult = new BookingActionResult()
                    {
                        ActionType = ActionType.Cancel,
                        Information = new BookingInformation() { Id = perma_id, JobNumber = perma_id, Status = "CLOSED" },
                        Success = true
                    };

                }
            }
            catch (Exception ex)
            {
                bookingActionResult = new BookingActionResult()
                {
                    ActionType = ActionType.New,
                    Error = new BookingError() { Code = ex.Source, Message = ex.Message },
                    Success = false
                };
            }

            return bookingActionResult;
        }
        private iCabbiBooking MapBooking(clsWebSupplierBooking booking)
        {
            int i = 0;
            var pickUplatlng = _helper.PostCodeToLatLng(booking.PickupAddressPostcodeFirstPart,
                booking.PickupAddressPostcodeSecondPart);
            var destUplatlng = _helper.PostCodeToLatLng(booking.DestAddressPostcodeFirstPart,
                booking.DestAddressPostcodeSecondPart);
            string notes = string.IsNullOrEmpty(booking.SpecialInstructions) == true ? "" : booking.SpecialInstructions;
            if (booking.CallOnArrival == "True")
                notes += " Call On Arrival";
            if (booking.WaitAndReturn == "True")
                notes += " WaitAndReturn";
            if (booking.MeetAndGreet == "True")
                notes += " Meet And Greet";
            if (booking.WheelChair == "True")
                notes += " WheelChair";

            iCabbiBooking iCabbiBooking = new iCabbiBooking
            {
               // perma_id = string.IsNullOrEmpty(booking.DespatchSystemBookingID)==true ? "" : booking.DespatchSystemBookingID,
                payment_type = "INVOICE",
                name = string.IsNullOrEmpty(booking.ClientLeadName)==true ? "": booking.ClientLeadName,
                phone = string.IsNullOrEmpty(booking.PassengerTelNo) == true ? "" : booking.PassengerTelNo,
                date = booking.PickupDateTime.ToString("s"),
                address = new address()
                {
                    formatted = booking.PickupAddressOneLine,
                    lat = pickUplatlng[0],
                    lng = pickUplatlng[1]
                },
                //address = new address() {formatted = "123 Fake Street", lat = "53.656598", lng = "-6.582641"},
                destination = new address()
                {
                    formatted = booking.PickupAddressOneLine,
                    lat = destUplatlng[0],
                    lng = destUplatlng[1]
                },
                flight_number = string.IsNullOrEmpty(booking.PickupFlightNo) == true ? "" : booking.PickupFlightNo,
                vehicle_type = booking.VehicleTypeDescription,
                instructions = notes
            };

            address[] vias;
            //var vias = new address[booking.BookingVias.Vias.Count];
            if (booking.BookingVias.Vias.Count > 0)
            {
                vias = new address[booking.BookingVias.Vias.Count];
                booking.BookingVias.Vias.ForEach(x =>
                {
                    var latlng = _helper.PostCodeToLatLng(x.Address.OutCode, x.Address.InCode);
                    vias[i] = new address() {lat = latlng[0], lng = latlng[1], formatted = x.Address.FullDescription};
                    i++;
                });
                iCabbiBooking.vias = vias;
            }
            //iCabbiBooking.vias = vias;
            return iCabbiBooking;
        }

        internal dynamic GetVehiclesTypes(clsWebSupplierDespatch wsd)
        {
            dynamic obj;
            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://stagingapi.icabbi.com/uk/config/vehicletypes?app_key=" + wsd.iCabbiApiKey),
                Content = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>{})
            };
            var response = _client.SendAsync(request).Result;
            var json = JObject.Parse(response.Content.ReadAsStringAsync().Result);
            obj = JsonConvert.DeserializeObject(json.ToString());
            return obj;
        }

        internal dynamic GetAccounts(clsWebSupplierDespatch wsd)
        {
            dynamic obj;
            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri =
                    new Uri(wsd.DespatchSystemURL + "accounts/index?username=test300&password=testfind300&app_key=" +
                            wsd.iCabbiApiKey),
                Content = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("username", "test300"),
                    new KeyValuePair<string, string>("password", "testfind300"),
                    new KeyValuePair<string, string>("secretkey", "a43e7f5d3ff31c8beedb5d1d685a2d1ea3b44954")
                })
            };
            var response = _client.SendAsync(request).Result;
            var json = JObject.Parse(response.Content.ReadAsStringAsync().Result);
            obj = JsonConvert.DeserializeObject(json.ToString());
            return obj.body.accounts;
        }
    }

    


    public class iCabbiBooking
    {
        public string id { get; set; }
        public string perma_id { get; set; }
        public string account_id { get; set; }
        public string payment_type { get; set; }
        public string user_id { get; set; }
        public string name { get; set; }
        public string phone { get; set; }
        public string date { get; set; }
        //public string eta { get; set; }
        public string flight_number { get; set; }
        public string vehicle_type { get; set; }
        public string instructions { get; set; }
        public address address { get; set; }
        public address destination { get; set; }
        public address[] vias { get; set; }
        public string status { get; set; }
       // public string notes { get; set; }
    }

    public class address
    {
   //     public string id { get { return ""; } }
        public string lat { get; set; }
        public string lng { get; set; }
        public string formatted { get; set; }
      //  public string descriptor { get { return ""; }}
    }

    public class booking
    {
        public string name { get; set; }
        public string phone { get; set; }
        public address address { get; set; }
        public string account_id { get; set; }
        public string user_id { get; set; }
        public string payment_type { get; set; }
    }
}
