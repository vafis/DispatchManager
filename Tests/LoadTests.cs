using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Threading;
using System.Threading.Tasks;
using Booking;
using Cabfind.Core.Types;
using Cabfind.DespatchManager;
using Cabfind.DespatchManager.Despatchers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xunit;
using Assert = Xunit.Assert;
using System.Threading;

namespace Despatchers.Tests
{
    public class BookingFixture : IDisposable
    {
        public BookingFixture() { }


        public static clsWebSupplierDespatch clsWebSupplierDespatch
        {
            get
            {
                return new clsWebSupplierDespatch()
                {
                    iCabbiApiKey = "8b515f4015dd007c3af9edfd8e4153686a67f531",
                    iCabbiSecretKey = "a43e7f5d3ff31c8beedb5d1d685a2d1ea3b44954",
                    iCabbiPhoneUser = "40723383454",
                    DespatchSystemURL = "https://stagingapi.icabbi.com/226/",
                    iCabbiUserName = "test300",
                    iCabbiPassword = "testfind300",
                    iCabbiAccountId = "3599",
                    iCabbiUserId = "300326",
                    AutoCabPassword = "eoqAHnX7",
                    AutoAgent = "166606",
                    AutoCabVendor = "766606",
                    CordicSourceSystem = "CABFIND",
                    CordicSourcePassword = "Charioteer55",
                    CordicSourceAccount = "CABFIND",
                    CordicTargetSystem = "Target2"
                };
            }
        }

        public static clsWebSupplierBooking clsWebSupplierBooking
        {
            get
            {
                var vias = new List<Via>();
                var via1 = new Via();
                via1.Address = new Address()
                {
                    AddressLine1 = "GRIMSBY DOCKS RAILWAY STATION",
                    AddressLine2 = "CLEETHORPES ROAD",
                    OutCode = "WS15",
                    InCode = "2ED"
                };
                var via2 = new Via();
                via2.Address = new Address()
                {
                    AddressLine1 = "GWERSYLLT RAILWAY STATION",
                    AddressLine2 = "HOPE STREET",
                    OutCode = "WS14",
                    InCode = "0PN"
                };
                // vias.Add(via1);
                // vias.Add(via2);

                return new clsWebSupplierBooking()
                {
                    DespatchSystemBookingID = "840732,840730,840731",
                    BookingID = 1565980,
                    CallOnArrival = "True",
                    ClientLeadName = "Kostas",
                    ClientPassengers = 1,
                    DestAddressOneLine = "MANCHESTER AIRPORT, ARRIVALS HALL, MANCHESTER, M90 1QX ",
                    DestAddressPostcodeFirstPart = "M90",
                    DestAddressPostcodeSecondPart = "1QX",
                    JobNo = 3557787,
                    MeetAndGreet = "False",
                    PassengerTelNo = "084363453",
                    PickupAddressOneLine =
                        "LIVERPOOL AIRPORT PLC, SOUTH TERMINAL, SPEKE HALL AVENUE, LIVERPOOL AIRPORT, L24 1YD ",
                    PickupAddressPostcodeFirstPart = "L24",
                    PickupAddressPostcodeSecondPart = "1YD",
                    PickupDateTime = new DateTime(2016, 3, 11, 12, 30, 0).ToUniversalTime(),
                    PickupFlightNo = "ESY3565",
                    PickupFlightTime = "11/03/2016 10:00",
                    PickupTerminal = "1",
                    SpecialInstructions = "notes11",
                    VehicleTypeDescription = "S",
                    NoOfVehicles = 1,
                    BookingVias = new BookingVias()
                    {
                        Vias = vias
                    },
                    WaitAndReturn = "True"
                };
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    [TestClass]
    public class LoadTests
    {
        private int numBookings = 3;
        private int msec = 0;
        private int times = 1;

       [Fact]
        public void Send_ICabbi()
        {
            var iCabbi = new ICabbi();
            var booking = BookingFixture.clsWebSupplierBooking;
            booking.NoOfVehicles = numBookings;
            for (var i = 0; i < times; i++)
            {
                var ret = iCabbi.SendBooking(booking, BookingFixture.clsWebSupplierDespatch);
                Assert.NotNull(ret);
                Assert.Equal(ret.Success, true);
                Thread.Sleep(msec);
            }
            Assert.Equal(1, 1);
        }

        [Fact]
        public void Send_AutoCab()
        {
            var booking = iCabbiFixture.clsWebSupplierBooking;
            var wsd = BookingFixture.clsWebSupplierDespatch;
            wsd.DespatchSystemURL = "https://cxs.autocab.net/api/agent";
            numBookings = 1;
            var bookingActionResult = new BookingActionResult();
            var tasks = new Task<BookingActionResult>[numBookings];
            booking.VehicleTypeDescription = "Saloon";
            for (var i = 0; i < numBookings; i++)
            {
                Task<BookingActionResult> t = SendAutoCab(booking, wsd);
                tasks.SetValue(t, i);
            }

            
            for (var j = 0; j < times; j++)
            {
                Task.Factory.ContinueWhenAll(tasks, (ret) =>
                {
                    var f = ret.ToList<Task<BookingActionResult>>().FirstOrDefault(x => x.Result.Success == false);
                    if (f == null)
                    {
                        bookingActionResult = new BookingActionResult()
                        {
                            Success = true
                        };
                    }
                    else
                    {
                        bookingActionResult = new BookingActionResult()
                        {
                            Success = false
                        };
                    }
                    Assert.Equal(bookingActionResult.Success, true);
                }).Wait();
                // Assert.Equal(bookingActionResult.Success, true);
                Thread.Sleep(msec);
            }
            //Assert.Equal(1, 1);
        }

        [Fact]
        public void Send_Cordic()
        {
            var booking = iCabbiFixture.clsWebSupplierBooking;
            var wsd = BookingFixture.clsWebSupplierDespatch;
            wsd.DespatchSystemURL = "http://81.105.223.86/cni";
            booking.WheelChair = "False";
            booking.CordicSupplierVehicle = "Saloon";

            var bookingActionResult = new BookingActionResult();
            var tasks = new Task<BookingActionResult>[numBookings];
            booking.VehicleTypeDescription = "Saloon";
            for (var i = 0; i < numBookings; i++)
            {
                Task<BookingActionResult> t = SendCordic(booking, wsd);
                tasks.SetValue(t, i);
            }

           for (var j = 0; j < times; j++)
            {
                Task.Factory.ContinueWhenAll(tasks, (ret) =>
                {
                    var f = ret.ToList<Task<BookingActionResult>>().FirstOrDefault(x => x.Result.Success == false);
                    if (f == null)
                    {
                        bookingActionResult = new BookingActionResult()
                        {
                            Success = true
                        };
                    }
                    else
                    {
                        bookingActionResult = new BookingActionResult()
                        {
                            Success = false
                        };
                    }
                    Assert.Equal(bookingActionResult.Success, true);
                }).Wait();
                // Assert.Equal(bookingActionResult.Success, true);
                Thread.Sleep(msec);
            }
            //Assert.Equal(1, 1);
        }

        private async Task<BookingActionResult> SendAutoCab(clsWebSupplierBooking booking, clsWebSupplierDespatch wsd)
        {
           var autoCab = new AutoCab();
           return await Task.Factory.StartNew(() => { return autoCab.SendBooking(booking, wsd); });
        }
        private async Task<BookingActionResult> SendCordic(clsWebSupplierBooking booking, clsWebSupplierDespatch wsd)
        {
            var cordic = new Cordic();
            return await Task.Factory.StartNew(() => { return cordic.SendBooking(booking, wsd); });
        }
        
    }
}
