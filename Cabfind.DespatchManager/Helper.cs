using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cabfind.DespatchManager
{
    public class Helper
    {
        private object syncLock = new object();
        private string _connetionString;

        public Helper()
        {
            _connetionString = ConfigurationManager.ConnectionStrings["PAFConnectionString"].ConnectionString;
        }


        public string[] PostCodeToLatLng(string outCode, string inCode)
        {
            string[] latlng = new string[] { "0", "0","" };
            string connetionString = null;
            SqlConnection cnn;
            SqlCommand cmd;
            string sql = null;
            SqlDataReader reader;

            cnn = new SqlConnection(_connetionString);
            try
            {
                lock (syncLock)
                {
                    //sql = "SELECT TOP 1 Latitude, Longitude FROM  dbo.PostCodeData";
                    //sql = sql + " WHERE (Outcode = N'" + outCode + "') AND (Incode = N'" + inCode + "')";
                    sql = "SELECT TOP 1 p.Latitude, p.Longitude,";
                    sql = sql +
                          " (SELECT Top 1 l.PostTown FROM Locality l where l.LocalityKey in ( select A.LocalityKey from Address A ";
                    sql = sql + " WHERE (A.Outcome = N'" + outCode + "') AND (A.Incode = N'" + inCode + "')";
                    sql = sql + " ) ) as Town FROM PostCodeData p ";
                    sql = sql + " WHERE (Outcode = N'" + outCode + "') AND (Incode = N'" + inCode + "')";

                    cnn.Open();
                    cmd = new SqlCommand(sql, cnn);
                    reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        latlng[0] = reader.GetValue(0).ToString();
                        latlng[1] = reader.GetValue(1).ToString();
                        latlng[2] = reader.GetValue(2).ToString();
                    }
                    reader.Close();
                    cmd.Dispose();
                    cnn.Close();
                }
            }
            catch (Exception ex)
            {

            }
            return latlng;
        }
    }
}
