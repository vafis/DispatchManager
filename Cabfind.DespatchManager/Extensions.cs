using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace Cabfind.DespatchManager
{
    public static class Extensions
    {
        public static DateTime GetAurigaDateTime(this DateTime pickupDateTime)
        {
            int month = pickupDateTime.Month;
            int year = pickupDateTime.Year;
            //  DayOfWeek dayfoOfWeek = pickupDateTime.DayOfWeek;

            DateTime startBST = new DateTime(year, 3, new GregorianCalendar().GetDaysInMonth(year, 3));
            startBST = ConvertBsTime(startBST);
            DateTime endBST = new DateTime(year, 10, new GregorianCalendar().GetDaysInMonth(year, 10));
            endBST = ConvertBsTime(endBST);

            if (DateTime.Compare(pickupDateTime, startBST) >= 0 && DateTime.Compare(pickupDateTime, endBST) <= 0)
            {
                pickupDateTime = pickupDateTime.AddHours(-1.00);
            }

            return pickupDateTime;

        }

        public static bool IsDayLightSaveDateTime(this DateTime pickupDateTime)
        {
            bool dateSave = false;
            int month = pickupDateTime.Month;
            int year = pickupDateTime.Year;
            //  DayOfWeek dayfoOfWeek = pickupDateTime.DayOfWeek;

            DateTime startBST = new DateTime(year, 3, new GregorianCalendar().GetDaysInMonth(year, 3));
            startBST = ConvertBsTime(startBST);
            DateTime endBST = new DateTime(year, 10, new GregorianCalendar().GetDaysInMonth(year, 10));
            endBST = ConvertBsTime(endBST);

            if (DateTime.Compare(pickupDateTime, startBST) >= 0 && DateTime.Compare(pickupDateTime, endBST) <= 0)
            {
                dateSave = true;
            }

            return dateSave;

        }

        private static DateTime ConvertBsTime(DateTime datetime)
        {
            for (; ; datetime = datetime.AddDays(-1))
            {
                if (datetime.DayOfWeek == DayOfWeek.Sunday)
                {
                    return datetime.AddHours(1);
                }
            }
        }
        public static DataTable GetAddisonLeeformatViaAddress(this Booking.clsWebSupplierBooking booking)
        {
            DataTable tblVias = new DataTable();
            string _connetionString = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;
            using (SqlConnection con = new SqlConnection(_connetionString))
            using (SqlCommand _cmd = new SqlCommand("USP_Cabvista_Address_RemoveCityNameIfRequested", con))
            {
                _cmd.CommandType = CommandType.StoredProcedure;

                _cmd.Parameters.Add(new SqlParameter("@BookingId", SqlDbType.Int));
                _cmd.Parameters["@BookingId"].Value = booking.BookingID;

                SqlDataAdapter dap = new SqlDataAdapter(_cmd);
                dap.Fill(tblVias);
            }
            return tblVias;
        }

        public static string[] GetAddisonLeeformatAddress(this Booking.clsWebSupplierBooking booking)
        {
            string[] formatted = new string[4];
            string _connetionString = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;
            using (SqlConnection con = new SqlConnection(_connetionString))
            {
                SqlCommand cmd = new SqlCommand("USP_Cabvista_PickupAddress_RemoveCityNameIfRequested", con);
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@BookingId", booking.BookingID);
                SqlParameter outPutParameter1 = new SqlParameter();
                outPutParameter1.ParameterName = "@ConcatenatedAddress1";
                outPutParameter1.SqlDbType = System.Data.SqlDbType.NVarChar;
                outPutParameter1.Size = 800;
                outPutParameter1.Direction = System.Data.ParameterDirection.Output;
                cmd.Parameters.Add(outPutParameter1);
                SqlParameter outPutParameter2 = new SqlParameter();
                outPutParameter2.ParameterName = "@ConcatenatedAddress2";
                outPutParameter2.SqlDbType = System.Data.SqlDbType.NVarChar;
                outPutParameter2.Size = 800;
                outPutParameter2.Direction = System.Data.ParameterDirection.Output;
                cmd.Parameters.Add(outPutParameter2);
                con.Open();
                cmd.ExecuteNonQuery();
                formatted[0] = outPutParameter1.Value.ToString().Trim();
                formatted[1] = outPutParameter2.Value.ToString().Trim();
            }
            using (SqlConnection con = new SqlConnection(_connetionString))
            {
                SqlCommand cmd = new SqlCommand("USP_Cabvista_DestAddress_RemoveCityNameIfRequested", con);
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@BookingId", booking.BookingID);
                SqlParameter outPutParameter1 = new SqlParameter();
                outPutParameter1.ParameterName = "@ConcatenatedAddress1";
                outPutParameter1.SqlDbType = System.Data.SqlDbType.NVarChar;
                outPutParameter1.Size = 800;
                outPutParameter1.Direction = System.Data.ParameterDirection.Output;
                cmd.Parameters.Add(outPutParameter1);
                SqlParameter outPutParameter2 = new SqlParameter();
                outPutParameter2.ParameterName = "@ConcatenatedAddress2";
                outPutParameter2.SqlDbType = System.Data.SqlDbType.NVarChar;
                outPutParameter2.Size = 800;
                outPutParameter2.Direction = System.Data.ParameterDirection.Output;
                cmd.Parameters.Add(outPutParameter2);
                con.Open();
                cmd.ExecuteNonQuery();
                formatted[2] = outPutParameter1.Value.ToString().Trim();
                formatted[3] = outPutParameter2.Value.ToString().Trim();
            }
            return formatted;
        }

    }
}
