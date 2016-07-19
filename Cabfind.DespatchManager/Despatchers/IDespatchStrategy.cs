using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Booking;

namespace Cabfind.DespatchManager.Despatchers
{
    public interface IDespatchStrategy
    {
        BookingActionResult SendBooking(clsWebSupplierBooking booking,clsWebSupplierDespatch wsd);
        BookingActionResult Update(clsWebSupplierBooking booking, clsWebSupplierDespatch wsd);
        BookingActionResult Cancel(clsWebSupplierBooking booking, clsWebSupplierDespatch wsd);
    }
}
