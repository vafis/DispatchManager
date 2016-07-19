using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Cabfind.DespatchManager
{
    /// <summary>
    /// The mapped "BookingActionResult" class with the returned object from
    /// each despatch system operations
    /// </summary>
    public class BookingActionResult
    {
        public BookingActionResult()
        {
            //Success = false;
        }
        public bool? Success { get; set; }
        public BookingInformation Information { get; set; }
        public BookingError Error { get; set; }
        public ActionType ActionType { get; set; }

    }
    /// <summary>
    /// Returned Booking Information
    /// </summary>
    public class BookingInformation
    {
        public string Id { get; set; }
        public string JobNumber { get; set; }
        public string Status { get; set; }
    }
    /// <summary>
    /// Booking Error
    /// </summary>
    public class BookingError
    {
        public string Code { get; set; }
        public string Message { get; set; }
    }

    public enum ActionType
    {
        New,
        Amend,
        Cancel
    }
}