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
    public class AddisonLeeTests : IClassFixture<AddisonLeeFixture>
    {
        private AddisonLeeFixture _fixture;

        public AddisonLeeTests(AddisonLeeFixture fixture)
        {
            _fixture = fixture;
        }

        [Theory]
        [InlineData(1)]
        // [InlineData(2)]
        public void Send_Booking(int count)
        {
            var addisonLee = new AddisonLee();
            var booking = _fixture.WebSupplierBooking;
            booking.NoOfVehicles = count;
            booking.CallOnArrival = "True";
            var ret = addisonLee.SendBooking(booking, _fixture.WebSupplierDespatch);
            Assert.NotNull(ret);
            Assert.Equal(ret.Success, true);
            Assert.NotNull(ret.Information);
            Assert.Null(ret.Error);
            Assert.Equal(ActionType.New, ret.ActionType);
        }

        [Theory]
        [InlineData(1)]
        public void Amend_Booking(int count)
        {
            var addisonLee = new AddisonLee();
            var booking = _fixture.WebSupplierBooking;
            booking.NoOfVehicles = count;
            var ret = addisonLee.SendBooking(booking, _fixture.WebSupplierDespatch);
            Assert.NotNull(ret);
            Assert.Equal(ret.Success, true);
            Assert.NotNull(ret.Information);
            Assert.Null(ret.Error);
            Assert.Equal(ActionType.New, ret.ActionType);
            booking.DespatchSystemBookingID = ret.Information.JobNumber;
            ret = addisonLee.Update(booking, _fixture.WebSupplierDespatch);
            Assert.NotNull(ret);
            Assert.Equal(ret.Success, true);

        }

        [Theory]
        [InlineData(1)]
        public void Amend_Booking2(int count)
        {
            var addisonLee = new AddisonLee();
            var booking = _fixture.WebSupplierBooking;
            booking.NoOfVehicles = count;
            var ret = addisonLee.SendBooking(booking, _fixture.WebSupplierDespatch);
            Assert.NotNull(ret);
            Assert.Equal(ret.Success, true);
            Assert.NotNull(ret.Information);
            Assert.Null(ret.Error);
            Assert.Equal(ActionType.New, ret.ActionType);
            booking.DespatchSystemBookingID = ret.Information.JobNumber;
            booking.NoOfVehicles = count;
            ret = addisonLee.Update(booking, _fixture.WebSupplierDespatch);
            Assert.NotNull(ret);
            Assert.Equal(ret.Success, true);

        }

        [Theory]
        [InlineData(1)]
        public void Cancel_Booking(int count)
        {
            var addisonLee = new AddisonLee();
            var booking = _fixture.WebSupplierBooking;
            booking.NoOfVehicles = count;
            var ret = addisonLee.SendBooking(booking, _fixture.WebSupplierDespatch);
            Assert.NotNull(ret);
            Assert.Equal(ret.Success, true);
            booking.DespatchSystemBookingID = ret.Information.JobNumber;
            ret = addisonLee.Cancel(booking, _fixture.WebSupplierDespatch);
            Assert.NotNull(ret);
            Assert.Equal(ret.Success, true);
            Assert.Equal(ret.Information.Status, "Cancelled");
        }

    }

    public class AddisonLeeFixture : IDisposable
    {
        public clsWebSupplierBooking WebSupplierBooking { get; private set; }
        public clsWebSupplierDespatch WebSupplierDespatch { get; private set; }


        public AddisonLeeFixture()
        {
            var vias = new List<Via>();
            var via1 = new Via();
            via1.Address = new Address()
            {
                AddressLine1 = "GRIMSBY DOCKS RAILWAY STATION",
                AddressLine2 = "CLEETHORPES ROAD",
                AddressLine3 = "Lincolnshire",
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

            WebSupplierBooking = new clsWebSupplierBooking()
            {
                // select * from Booking where jobno=3774561
                DespatchSystemBookingID = "840731",
                BookingID = 1565980,
                CallOnArrival = "True",
                ClientLeadName = "Kostas",
                ClientPassengers = 1,
                DestAddressOneLine = "14 BALMES ROAD  N1 3JE",
                DestAddressLine4 = "London",
                DestAddressPostcodeFirstPart = "N1",
                DestAddressPostcodeSecondPart = "3JE",
                JobNo = 3557787,
                MeetAndGreet = "False",
                PassengerTelNo = "084363453",
                PickupAddressOneLine =
                    // "LIVERPOOL AIRPORT PLC, SOUTH TERMINAL, SPEKE HALL AVENUE, LIVERPOOL AIRPORT, L24 1YD ",
                   "7 FAIRSTEAD WALK, POPHAM STREET  N1 8QU",
                PickupAddressLine4 = "London",
                PickupAddressPostcodeFirstPart = "N1",
                PickupAddressPostcodeSecondPart = "8QU",
                PickupDateTime = new DateTime(2016, 05, 30, 12, 30, 0).ToUniversalTime(),
                PickupFlightNo = "ESY3565",
                PickupFlightTime = "31/08/2015 10:00",
                PickupTerminal = "1",
                PickupBookingFlightID = 20,
                SpecialInstructions = "notes11",
                VehicleTypeDescription = "OneFourPassengers",
                AddisonLeeSupplierVehicle = "OneFourPassengers",
                NoOfVehicles = 1,
                BookingVias = new BookingVias()
                {
                    Vias = vias
                },
                WaitAndReturn = "True",
                AccountCode = "7f9cfdb7-1c87-4af1-99a4-df843eb102eb",
                AccountNoToken = "7f9cfdb7-1c87-4af1-99a4-df843eb102eb",
                
                
                
            };
            WebSupplierDespatch = new clsWebSupplierDespatch()
            {
                iCabbiApiKey = "8b515f4015dd007c3af9edfd8e4153686a67f531",
                iCabbiSecretKey = "a43e7f5d3ff31c8beedb5d1d685a2d1ea3b44954",
                iCabbiPhoneUser = "40723383454",
                DespatchSystemURL = "https://apps.addisonlee.com/v2-sandbox/smartphones/App2",
                iCabbiUserName = "test300",
                iCabbiPassword = "testfind300",
                iCabbiAccountId = "3599",
                iCabbiUserId = "300326",
                AddisonLeeAppId = "7cca09fb-8e45-4d54-9ac2-8466dffda00f",
                AddisonLeeToken = "p/j1C5tLlGi3ZvPSzPdP4wfS6Nxi/RvZCkvxTyHASajoEbv1DbmAxzmNl55ZTQESJ8JF1/co3eBu\\nW5FMuCr36tublUzyLnNa"

            };
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

}
