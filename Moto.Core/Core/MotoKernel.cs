// Core/MotoKernel.cs
using System;
using System.Collections.Generic;

namespace Moto.Editor.Core
{
    /// <summary>
    /// Noyau de services minimal pour MOTO Editor.
    /// Permet d'injecter les moteurs sans dépendance externe.
    /// </summary>
    public sealed class MotoKernel
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        /// <summary>
        /// Enregistre un service.
        /// </summary>
        public void Register<TService>(TService implementation)
            where TService : class
        {
            _services[typeof(TService)] = implementation;
        }

        /// <summary>
        /// Récupère un service enregistré.
        /// </summary>
        public TService Get<TService>()
            where TService : class
        {
            return (TService)_services[typeof(TService)];
        }

        /// <summary>
        /// Tente de récupérer un service.
        /// </summary>
        public bool TryGet<TService>(out TService service)
            where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out var value))
            {
                service = (TService)value;
                return true;
            }

            service = null;
            return false;
        }
    }
}
