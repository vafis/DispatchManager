using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Booking;
using System.Net;
using System.Configuration;
using Cabfind.DespatchManager;
using System.Diagnostics;

namespace Cabfind.DespatchManager.Despatchers
{
    /// <summary>
    /// Auriga Despatcher Class
    /// </summary>
    public class Auriga : IDespatchStrategy
    {

        private AurigaWS.BookingService _bookingService = new AurigaWS.BookingService();
        //private AurigaWS.BookingResult[] _bookingresult = new AurigaWS.BookingResult[10];
        private AurigaWS.BookingResult[] _bookingresult;
        private AurigaWS.BookingResult _updateResult;
        private AurigaWS.Booking _aurigaBooking = new AurigaWS.Booking();
        //CredentialCache myCache = new CredentialCache();

        //If dispatch manager should called by another application set the following username/password hard coded
        //Its common for everyone
        private string _WebServiceUsername = ConfigurationManager.AppSettings["AurigaLevel1Username"].ToString();
        private string _WebServicePassword = ConfigurationManager.AppSettings["AurigaLevel1Password"].ToString();


        private clsWebSupplierDespatch _wsd;
        private clsWebSupplierBooking _caspaBooking;
        private BookingActionResult _bookingActionResult = new BookingActionResult();

        private Func<AurigaWS.BookingResult[], string> FormatIds = delegate(AurigaWS.BookingResult[] bookingResults)
        {
            var idList = new List<AurigaWS.BookingResult>(bookingResults.Length);
            idList.AddRange(bookingResults);

            string ret = "";
            idList.ForEach(x =>
            {
                ret = ret + x.information.id + ",";

            });
            return ret.TrimEnd(',');
        };

        private Func<AurigaWS.BookingResult[], string> FormatJobNumbers = delegate(AurigaWS.BookingResult[] bookingResults)
        {
            var idList = new List<AurigaWS.BookingResult>(bookingResults.Length);
            idList.AddRange(bookingResults);

            string ret = "";
            idList.ForEach(x =>
            {
                ret = ret + x.information.jobNumber + ",";
            });
            return ret.TrimEnd(',');
        };




        public Auriga()
        {

        }
        /// <summary>
        /// Cancel an existing booking to Auriga system
        /// </summary>
        public BookingActionResult Cancel(clsWebSupplierBooking booking, clsWebSupplierDespatch wsd)
        {
            _caspaBooking = booking;
            _wsd = wsd;

            if (Login())
            {
                CancelBooking(_caspaBooking.DespatchSystemBookingID);
                Logout();
            }
            else
            {
                _bookingActionResult.Error = new BookingError { Message = "Unable to login at Auriga Despatch System" };

            }
            return _bookingActionResult;

        }

        /// <summary>
        /// Update the booking to Auriga System
        /// </summary>
        public BookingActionResult Update(clsWebSupplierBooking booking, clsWebSupplierDespatch wsd)
        {
            _caspaBooking = booking;
            _wsd = wsd;

            //Map Caspa Booking to Auriga Booking object
            init();

            if (Login())
            {
                List<string> aurigaIds = _caspaBooking.DespatchSystemBookingID.Split(',').ToList();

                //Is multiple cars?
                if (aurigaIds.Count > 1 || _caspaBooking.NoOfVehicles > 1)
                {
                    aurigaIds.ForEach(x =>
                    {
                        CancelBooking(x.ToString());
                    });
                    Book();
                }
                else //just one booking
                {
                    UpDateBooking();
                }

                Logout();
            }
            else
            {
                _bookingActionResult.Error = new BookingError { Message = "Unable to login at Auriga Despatch System" };
            }
            return _bookingActionResult;


        }
        /// <summary>
        /// Send a new booking the booking to Auriga System
        /// </summary>
        public BookingActionResult SendBooking(clsWebSupplierBooking booking, clsWebSupplierDespatch wsd)
        {
            _caspaBooking = booking;
            _wsd = wsd;

            //Map Caspa Booking to Auriga Booking object
            init();

            if (Login())
            {
                Book();
                Logout();
            }
            else
            {

                _bookingActionResult.Success = false;
                _bookingActionResult.Error = new BookingError { Code = "Caspa", Message = "Unable to login at Auriga Despatch System" };

            }
            return _bookingActionResult;
        }

        /// <summary>
        /// 
        /// </summary>
        private void init()
        {
            // Prab's recommendations
            _bookingService.AllowAutoRedirect = true;
            _bookingService.CookieContainer = new CookieContainer();

            _bookingService.Url = _wsd.DespatchSystemURL;

            CredentialCache myCache = new CredentialCache();
            myCache.Add(new Uri(_bookingService.Url), "Basic", new NetworkCredential(_WebServiceUsername, _WebServicePassword));
            _bookingService.Credentials = myCache;
            _bookingService.PreAuthenticate = true;

            AurigaWS.Address[] addresses;

            //Prab says we don't need to set these up
            //m_booking.id = wsb.BookingID.ToString();
            //m_booking.jobNumber = wsb.JobNo.ToString();

            //Add 10 minutes window for ASAP bookings
            if (_caspaBooking.PickupDateTime.ToUniversalTime() > DateTime.UtcNow.AddMinutes(10.00))
            {
                _aurigaBooking.pickupDateTime = _caspaBooking.PickupDateTime.ToUniversalTime();
            }
            //else set _aurigaBooking.pickupDateTime blank for ASAP bookings


            //Passenger
            AurigaWS.Contact aurigaPassenger = new AurigaWS.Contact();
            aurigaPassenger.name = _caspaBooking.ClientLeadName;
            aurigaPassenger.phone = _caspaBooking.PassengerTelNo;
            _aurigaBooking.passenger = aurigaPassenger;

            //Pickup Address
            AurigaWS.Address pickup = new AurigaWS.Address();

            //build the pickupaddress in an address field but without the postcode
            pickup = ProcessAddress(_caspaBooking.PickupAddressOneLine.Replace(_caspaBooking.PickupAddressPostcodeFirstPart + " " + _caspaBooking.PickupAddressPostcodeSecondPart, ""));
            //now add the postcode
            pickup.postCode = _caspaBooking.PickupAddressPostcodeFirstPart + _caspaBooking.PickupAddressPostcodeSecondPart;






            //Destination Address
            AurigaWS.Address destination = new AurigaWS.Address();

            //build the destination in an address field but without the postcode
            destination = ProcessAddress(_caspaBooking.DestAddressOneLine.Replace(_caspaBooking.DestAddressPostcodeFirstPart + " " + _caspaBooking.DestAddressPostcodeSecondPart, ""));
            //now add the postcode
            destination.postCode = _caspaBooking.DestAddressPostcodeFirstPart + _caspaBooking.DestAddressPostcodeSecondPart;

            if (_caspaBooking.BookingVias.Vias.Count > 0)
            {
                addresses = new AurigaWS.Address[_caspaBooking.BookingVias.Vias.Count + 2];
                addresses[0] = pickup;

                for (int i = 1; i < _caspaBooking.BookingVias.Vias.Count + 1; i++)
                {
                    AurigaWS.Address viaAddress = new AurigaWS.Address();
                    viaAddress.buildingNameorNumber = _caspaBooking.BookingVias.Vias[i - 1].Address.AddressLine1 == "" ? _caspaBooking.BookingVias.Vias[i - 1].Address.AddressLine2 : _caspaBooking.BookingVias.Vias[i - 1].Address.AddressLine1;
                    viaAddress.streetName = _caspaBooking.BookingVias.Vias[i - 1].Address.AddressLine2;
                    viaAddress.townOrDistrict = _caspaBooking.BookingVias.Vias[i - 1].Address.AddressLine3;
                    viaAddress.postCode = _caspaBooking.BookingVias.Vias[i - 1].Address.OutCode + _caspaBooking.BookingVias.Vias[i - 1].Address.InCode;
                    addresses[i] = viaAddress;

                    //addresses[i] = GetVIAAddress(_caspaBooking.VIAs[i - 1]);
                }

                addresses[addresses.Count() - 1] = destination;
            }
            else
            {
                addresses = new AurigaWS.Address[2];
                addresses[0] = pickup;
                addresses[1] = destination;
            }

            _aurigaBooking.addresses = addresses;

            _aurigaBooking.notification = _caspaBooking.CallOnArrival == "True" ? "Call" : "";
            _aurigaBooking.notes = GetSpecialInstructions(_caspaBooking);
            _aurigaBooking.numberOfVehicles = _caspaBooking.NoOfVehicles;
            // TODO - Stop adding preferences to the end of the notes when they fix their bug
            // tack preferences onto the end of the notes cos there's a bug at the mp
            //  _aurigaBooking.preferences = GetPreferences(_caspaBooking);
            _aurigaBooking.preferences = new string[1] { _caspaBooking.AurigaSupplierVehicle.Trim() };
            //          //  _aurigaBooking.notes += ", " + _aurigaBooking.preferences[0];
            //           // _aurigaBooking.preferences = null;
            _aurigaBooking.clientReference = _caspaBooking.ClientRefNo;

            //Prab extras
            _aurigaBooking.paymentType = AurigaWS.PaymentType.ACCOUNT;
            AurigaWS.Account aurigaAccount = new AurigaWS.Account();
            aurigaAccount.name = _wsd.AurigaAccountName;
            _aurigaBooking.account = aurigaAccount;

            //Build Pickup Flight Details only
            if (_caspaBooking.PickupBookingFlightID != 0)
            {
                AurigaWS.FlightInfo flightInfo = new AurigaWS.FlightInfo();
                flightInfo.identifier = _caspaBooking.PickupFlightNo;
                flightInfo.destination = _caspaBooking.PickupTerminal;
                flightInfo.time = _caspaBooking.PickupFlightTime.ToString();
                _aurigaBooking.flightInfo = flightInfo;
            }



        }



        // This is passed a comma concatenated address without the postcode
        // and populates an Auriga address object with the various address elements
        private AurigaWS.Address ProcessAddress(string AddressOneLine)
        {
            AurigaWS.Address address = new AurigaWS.Address();
            int intCommas = AddressOneLine.ToCharArray().Count(c => c == ',');
            string[] strAddressOneline = AddressOneLine.Split(',');

            for (int i = 0; i < intCommas; i++)
            {
                if (string.IsNullOrEmpty(address.buildingNameorNumber))
                {
                    address.buildingNameorNumber = strAddressOneline[i];
                }
                else if (string.IsNullOrEmpty(address.streetName))
                {
                    address.streetName = strAddressOneline[i];
                }
                else if (string.IsNullOrEmpty(address.townOrDistrict))
                {
                    address.townOrDistrict = strAddressOneline[i];
                }
                else
                {
                    break;
                }
            }

            return address;
        }




        /*
          private AurigaWS.Address GetVIAAddress(clsBookingVia VIA)
          {
              AurigaWS.Address VIAAddress = new AurigaWS.Address();

              //build the VIAAddress in an address field but without the postcode
              VIAAddress = ProcessAddress(VIA.AddressOneLine.Replace(VIA.AddressPostcodeFirstPart + " " + VIA.AddressPostcodeSecondPart, ""));
              //now add the postcode
              VIAAddress.postCode = VIA.AddressPostcodeFirstPart + " " + VIA.AddressPostcodeSecondPart;
              return VIAAddress;
          }
        */

        private string[] GetPreferences(clsWebSupplierBooking wsb)
        {
            string[] preferences = new string[1];
            /*
         
            if (wsb.VehicleTypeDescription.Contains("Saloon"))
            {
                preferences = AddToInstructions(preferences, "SALOON");
            }
            else if (wsb.VehicleTypeDescription.Contains("Executive"))
            {
                preferences = AddToInstructions(preferences, "EXECUTIVE");
            }
             * */

            return preferences;
        }

        private string GetSpecialInstructions(clsWebSupplierBooking wsb)
        {
            string instructions = ""; // = wsb.SpecialInstructions;

            if (wsb.WaitAndReturn == "True")
            {
                instructions += ",Wait&Return";
            }
            if (wsb.WheelChair == "True")
            {
                instructions += ",Wheelchair";
            }
            if (wsb.MeetAndGreet == "True")
            {
                instructions += ",Meet&Greet";
            }
            /*
            if (wsb.CallOnArrival == "True")
            {
                instructions += ",Call";
            }
             */
            if (instructions.Length > 0)
            {
                instructions += ",";
            }

            instructions += wsb.SpecialInstructions;

            if (instructions.StartsWith(","))
            {
                instructions = instructions.Substring(1, instructions.Length - 1);
            }
            return instructions.Trim();
        }

        private string[] AddToInstructions(string[] Instructions, string Instruction)
        {
            for (int i = 0; i < 4; i++)
            {
                if (String.IsNullOrEmpty(Instructions[i]))
                {
                    Instructions[i] = Instruction;
                    break;
                }
            }
            return Instructions;
        }

        private bool Login()
        {

            _bookingService.AllowAutoRedirect = true;
            _bookingService.CookieContainer = new CookieContainer();

            _bookingService.Url = _wsd.DespatchSystemURL;

            CredentialCache myCache = new CredentialCache();
            myCache.Add(new Uri(_bookingService.Url), "Basic", new NetworkCredential(_WebServiceUsername, _WebServicePassword));
            _bookingService.Credentials = myCache;
            _bookingService.PreAuthenticate = true;

            bool bLoggedIn = false;
            int intFailCount = 0;

            while ((!bLoggedIn) && (intFailCount < 5))
            {
                try
                {
                    bLoggedIn = _bookingService.Login(_wsd.AurigaUsername, _wsd.AurigaPassword, _wsd.AurigaPartnerName);
                }
                catch
                {
                    intFailCount++;
                }
            }
            return bLoggedIn;
        }

        private void CancelBooking(string despatchSystemBookingID)
        {
            try
            {
                AurigaWS.BookingStatus bookingstatus = _bookingService.Cancel(despatchSystemBookingID);
                if (bookingstatus == AurigaWS.BookingStatus.CANCELLED)
                {
                    _bookingActionResult.Success = true;
                    _bookingActionResult.ActionType = ActionType.Cancel;
                    _bookingActionResult.Information = new BookingInformation { Status = bookingstatus.ToString(), Id = despatchSystemBookingID, JobNumber = "CANCELLED" };
                }
                else
                {
                    _bookingActionResult.Success = false;
                    _bookingActionResult.ActionType = ActionType.Cancel;
                    _bookingActionResult.Error = new BookingError { Code = "Caspa", Message = "Cancel does not completed" };
                }

            }
            catch (Exception ex)
            {
                _bookingActionResult.Success = false;
                _bookingActionResult.Error = new BookingError { Code = "Caspa", Message = ex.Message };
            }

        }

        private void UpDateBookings()
        {

        }

        private void UpDateBooking()
        {

            //string[] ids = _caspaBooking.DespatchSystemBookingID.Split(',');

            try
            {

                _updateResult = _bookingService.Update(_caspaBooking.DespatchSystemBookingID, _aurigaBooking);

                if (_updateResult.success)
                {
                    _bookingActionResult.Success = true;
                    _bookingActionResult.ActionType = ActionType.Amend;
                    _bookingActionResult.Information = new BookingInformation
                    {
                        Id = _updateResult.information.id,
                        JobNumber = _updateResult.information.jobNumber,
                        Status = _updateResult.information.status.ToString()
                    };
                }
                else
                {
                    _bookingActionResult.Success = _updateResult.success;
                    _bookingActionResult.ActionType = ActionType.Amend;
                    _bookingActionResult.Error = new BookingError
                    {
                        Code = _updateResult.error.code.ToString(),
                        Message = _updateResult.error.message
                    };
                }

            }
            catch (Exception ex)
            {
                _bookingActionResult.Success = false;
                _bookingActionResult.ActionType = ActionType.Amend;
                _bookingActionResult.Error = new BookingError { Code = "Caspa", Message = ex.Message };
            }

        }

        private void Book()
        {
            try
            {
                _bookingresult = _bookingService.Book(_aurigaBooking);

                if (_bookingresult[0].success)
                {
                    _bookingActionResult.Success = true;
                    _bookingActionResult.ActionType = ActionType.New;
                    _bookingActionResult.Information = new BookingInformation
                    {
                        Id = FormatIds(_bookingresult),  //_bookingresult[0].information.id,
                        JobNumber = FormatJobNumbers(_bookingresult),  //_bookingresult[0].information.jobNumber,
                        Status = _bookingresult[0].information.status.ToString()
                    };
                }
                else
                {
                    _bookingActionResult.Success = _bookingresult[0].success;
                    _bookingActionResult.ActionType = ActionType.New;
                    _bookingActionResult.Error = new BookingError
                    {
                        Code = _bookingresult[0].error.code.ToString(),
                        Message = _bookingresult[0].error.message
                    };
                }
            }
            catch (Exception ex)
            {
                _bookingActionResult.Success = false;
                _bookingActionResult.ActionType = ActionType.New;
                _bookingActionResult.Error = new BookingError { Code = "Caspa", Message = ex.Message };
            }
        }

        private void Logout()
        {
            try
            {
                _bookingService.Logout();
            }
            catch (Exception ex)
            {

            }
        }

    }
}
