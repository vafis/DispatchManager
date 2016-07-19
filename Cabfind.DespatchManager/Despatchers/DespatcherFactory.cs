using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace Cabfind.DespatchManager.Despatchers
{
    /// <summary>
    /// Despatcher Factory
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DespatcherFactory<T> : IDespatcherFactory where T : IDespatchStrategy, new()
    {
      
        private readonly T _despatcher;

        public DespatcherFactory()
        {
            _despatcher = new T();
        }
       
        public IDespatchStrategy GetDespatcher()
        {
            return _despatcher; 
        }
    }

    public interface IDespatcherFactory
    {
        IDespatchStrategy GetDespatcher();
    }
}
