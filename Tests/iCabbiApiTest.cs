using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Booking;
using Cabfind.DespatchManager;
using Cabfind.DespatchManager.Despatchers;
using Cabfind.Server.Data;
using Cabfind.Core.Types;
using Moq;
using Xunit;

namespace Despatchers.Tests
{
    public class iCabbiFixture : IDisposable
    {
        public iCabbiFixture() { }


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
                    iCabbiUserId = "300326"
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
                    DespatchSystemBookingID = "840731", //840732,840730,
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
                    PickupDateTime = new DateTime(2016, 8, 31, 12, 30, 0).ToUniversalTime(),
                    PickupFlightNo = "ESY3565",
                    PickupFlightTime = "31/08/2015 10:00",
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

    public class iCabbiFixtureTests : IClassFixture<iCabbiFixture>
    {
        iCabbiFixture fixture;

        public iCabbiFixtureTests(iCabbiFixture fixture)
        {
            this.fixture = fixture;
        }

    }


    public class iCabbiApiTest
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void Cancel_Booking_SuccessFully_without_error_Test(int count)
        {
            var iCabbi = new ICabbi();
            var booking = iCabbiFixture.clsWebSupplierBooking;
            booking.NoOfVehicles = count;
            var ret = iCabbi.SendBooking(booking, iCabbiFixture.clsWebSupplierDespatch);
            Assert.NotNull(ret);
            Assert.Equal(ret.Success, true);
            booking.DespatchSystemBookingID = ret.Information.Id;
            ret = iCabbi.Cancel(booking, iCabbiFixture.clsWebSupplierDespatch);

            Assert.NotNull(ret);
            Assert.Equal(ret.Success, true);
            Assert.Equal(ret.Information.Status, "CLOSED");
            Assert.Equal(ActionType.Cancel, ret.ActionType);
        }
        
        [Theory]
        [InlineData(1,1, "notes","new notes")]
        [InlineData(2, 1, "notes", "new notes")]
        public void Update_Booking_SuccessFully_without_error_Test(int count1,int count2, string notes1, string notes2)
        {
            var iCabbi = new ICabbi();
            var booking = iCabbiFixture.clsWebSupplierBooking;
            booking.NoOfVehicles = count1;
            booking.SpecialInstructions = notes1;
            var retCreate = iCabbi.SendBooking(booking,
                                               iCabbiFixture.clsWebSupplierDespatch);
            booking.DespatchSystemBookingID = retCreate.Information.Id;
            booking.PickupDateTime = booking.PickupDateTime.AddDays(1.0);
            booking.NoOfVehicles = count2;
            booking.SpecialInstructions = notes2;
            var retUpdate = iCabbi.Update(booking, iCabbiFixture.clsWebSupplierDespatch);

            Assert.NotNull(retUpdate);
            Assert.Equal(retUpdate.Success, true);
            Assert.Equal(ActionType.Amend,retUpdate.ActionType);
            Assert.Null(retUpdate.Error);
            if (count1 != count2){
                Assert.NotEqual(retCreate.Information.Id, retUpdate.Information.Id);
            } else {
                Assert.Equal(retCreate.Information.Id, retUpdate.Information.Id);
            }

        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void Send_Booking_SuccessFully_without_error_Test(int count)
        {
            var iCabbi = new ICabbi();
            var booking = iCabbiFixture.clsWebSupplierBooking;
            booking.NoOfVehicles = count;
            var ret = iCabbi.SendBooking(booking,
                iCabbiFixture.clsWebSupplierDespatch);
            Assert.NotNull(ret);
            Assert.Equal(ret.Success, true);
            Assert.NotNull(ret.Information);
            Assert.Null(ret.Error);
            Assert.Equal(ActionType.New, ret.ActionType);
        }

        [Fact]
        public void Send_Booking_SuccessFully_with_error_Test()
        {
            var iCabbi = new ICabbi();
            var settings = iCabbiFixture.clsWebSupplierDespatch;
            settings.iCabbiSecretKey = "wrongSercetKey";
            var ret = iCabbi.SendBooking(iCabbiFixture.clsWebSupplierBooking,
                settings);
            Assert.NotNull(ret);
            Assert.Equal(ret.Success, false);
            Assert.NotNull(ret.Error);
        }

        [Fact]
        public void Can_Get_VehiclesTypes()
        {
            var icabbi = new ICabbi();
            dynamic ret = icabbi.GetVehiclesTypes(iCabbiFixture.clsWebSupplierDespatch);
            Assert.NotNull(ret);
        }
        [Fact]
        public void Can_Get_Accounts()
        {
            var icabbi = new ICabbi();
            dynamic ret = icabbi.GetAccounts(iCabbiFixture.clsWebSupplierDespatch);
            Assert.NotNull(ret);
        }
    }
}
