using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Booking;

namespace Cabfind.DespatchManager
{


    ///<summary>
    ///Despatcher Manager
    ///</summary>
    ///<remarks>
    ///Despatcher Manager Class.
    /// Used for Despatch, Modify, Cancel Despatcher commands/procedures
    ///</remarks>
    public class Despatcher:DespatcherBase
    {
        BookingActionResult _bookingActionResult;

        public Despatcher(string connectionString, int supplierId)
            :base( connectionString,  supplierId)
        {
           
        }
        /// <summary>
        /// Cancel Booking Command
        /// </summary>
        /// <param name="clsWebSupplierBooking">clsWebSupplierBooking class</param>
        public override BookingActionResult Cancel(clsWebSupplierBooking booking)
        {
            this.clsWebSupplierBooking = booking;
            try
            {
                // string despatchSystemBooingID = booking.GetDespatchSystemBookingID(this._conStr, booking.BookingID, this.DespatchSystemID, booking.RevisionNo);
                _bookingActionResult = DespatchStrategy.Cancel(booking, this.clsWebSupplierDespatch);
            }
            catch (Exception ex)
            {
                _bookingActionResult = new BookingActionResult() { Error = new BookingError() { Message = ex.Message } };
            }
            UpdateCaspa(_bookingActionResult);
            return _bookingActionResult;
        }

        /// <summary>
        /// Update Booking Command
        /// </summary>
        /// <param name="clsWebSupplierBooking">clsWebSupplierBooking class</param>
        public override BookingActionResult Update(clsWebSupplierBooking booking)
        {
            this.clsWebSupplierBooking = booking;
            try
            {
                //Get the Despatch System Booking ID
             //   string despatchSystemBooingID = booking.GetDespatchSystemBookingID(this._conStr, booking.BookingID, this.DespatchSystemID, booking.RevisionNo-1);
                //Update booking to Despatch System
                _bookingActionResult = DespatchStrategy.Update(booking, this.clsWebSupplierDespatch);
               
            }
            catch (Exception ex)
            {
                _bookingActionResult = new BookingActionResult() { Error = new BookingError() { Message = ex.Message } };
            }

            UpdateCaspa(_bookingActionResult);
            return _bookingActionResult;
        }

        /// <summary>
        /// Despatch Booking Command
        /// </summary>
        /// <param name="clsWebSupplierBooking">clsWebSupplierBooking class</param>
        public override BookingActionResult Despatch(clsWebSupplierBooking booking)
        {
            this.clsWebSupplierBooking = booking;
            try
            { 
                //send booking to Despatch System
                _bookingActionResult = DespatchStrategy.SendBooking(booking, this.clsWebSupplierDespatch);
                
            }
            catch(Exception ex)
            {
                _bookingActionResult = new BookingActionResult() {Error = new BookingError() {Message = ex.Message}};
            }
            UpdateCaspa(_bookingActionResult);

            return _bookingActionResult;
        }

        /// <summary>
        /// Update Caspa info / DataBase according the result of Despatch, Modify, Cancel Despatcher class commands/procedures 
        /// </summary>
        /// <param name="BookingActionResult">BookingActionResult class</param>
        private void UpdateCaspa(BookingActionResult bookingActionResult)
        {
            string response;
            //Have return by web service

            if (bookingActionResult.Success != null)
            {
                response = bookingActionResult.Success == true
                    ? "Booking successfully " + bookingActionResult.ActionType.ToString() + " ID: " +
                      bookingActionResult.Information.Id + ". JobNumber: " + bookingActionResult.Information.JobNumber +
                      ". Status: " + bookingActionResult.Information.Status
                    : "Error trying to create booking. Code: " + bookingActionResult.Error + ". Message: " +
                      bookingActionResult.Error.Message;
            }
            else
            {
                response = string.IsNullOrEmpty(bookingActionResult.Error.Message) ? "InternalError" :  bookingActionResult.Error.Message;                
            }
            /*
            if (bookingActionResult.Success != null)
            {
                response = bookingActionResult.Success == true ? "Booking successfully created. ID: " + bookingActionResult.Information.Id + ". JobNumber: " + bookingActionResult.Information.JobNumber + ". Status: " + bookingActionResult.Information.Status :
                                                                       "Error trying to create booking. Code: " + bookingActionResult.Error + ". Message: " + bookingActionResult.Error.Message;
            }
            else
            {
                response = "Booking Cancelled. ID: " + bookingActionResult.Information.Id + ". JobNumber: " + bookingActionResult.Information.JobNumber + ". Status: " + bookingActionResult.Information.Status;
            }
            */
            clsWebSupplierBooking.UpdateSentToDespatchSystem(_conStr,
                                                             DespatchSystemID,
                                                             this.clsWebSupplierBooking.RevisionNo,
                                                             _bookingActionResult.Information == null ? null : _bookingActionResult.Information.Id,
                                                             response,
                                                             DateTime.Now,
                                                             _bookingActionResult.Information == null ? null : _bookingActionResult.Information.JobNumber);
     
        }
    }
}
