using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cabfind.DespatchManager.Despatchers;
using Booking;
using System.Reflection;
using System.Runtime.Remoting;

namespace Cabfind.DespatchManager
{
    /// <summary>
    /// Despatch Systems Enum
    /// </summary>
    public enum DespacthStrategy
    {
        Auriga = 1,
        Corbic = 2
    }

    /// <summary>
    /// Abstract Class DespatcherBase 
    /// </summary>
    public abstract class DespatcherBase
    {
        private const string _baseNamespace = "Cabfind.DespatchManager.Despatchers.";
        private clsWebSupplierDespatch _wsd = new clsWebSupplierDespatch();
        protected string _conStr;

        protected DespatcherBase(string connectionString, int supplierId)
        {
            _conStr = connectionString;
            _wsd.SelectSuppliersDespatchSystemBySupplierID(_conStr, supplierId);
            this.clsWebSupplierDespatch = _wsd;
            this.DespatchSystemID = _wsd.DespatchSystemID;
            /*
            // ************ 1st initialization methodology ***********
                        ObjectHandle handle = Activator.CreateInstance(null, _baseNamespace + _wsd.DespatchSystemName);
                        IDespatchStrategy despatcher = (IDespatchStrategy)handle.Unwrap();
                        this.DespatchStrategy = despatcher;
            // ************ 1st initialization methodology ***********
            */


            // ************ USING FACTORY DESPACHER ***********
            Type t = Type.GetType(_baseNamespace + "DespatcherFactory`1");
            Type d = Type.GetType(_baseNamespace + _wsd.DespatchSystemName);
            Type[] typeArgs = { d };
            Type makeme = t.MakeGenericType(typeArgs);
            IDespatcherFactory factory= Activator.CreateInstance(makeme) as IDespatcherFactory;
            this.DespatchStrategy = factory.GetDespatcher();
          // ************ END OF USING FACTORY DESPACHER ***********

        }

        protected clsWebSupplierDespatch clsWebSupplierDespatch { get; set; }
        protected clsWebSupplierBooking clsWebSupplierBooking { get; set; }
        protected IDespatchStrategy Despatcher { get; set; }
        protected int DespatchSystemID { get; set; }
        protected IDespatchStrategy DespatchStrategy { get; set; }
        public abstract BookingActionResult Despatch(clsWebSupplierBooking booking);
        public abstract BookingActionResult Update(clsWebSupplierBooking booking);
        public abstract BookingActionResult Cancel(clsWebSupplierBooking booking);
         
    }


}
