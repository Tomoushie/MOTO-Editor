// Snake2000.Engine/AgentIntegrated/Pipeline/PipelineStepException.cs
using System;

namespace Snake2000.Engine.AgentIntegrated.Pipeline
{
    /// <summary>
    /// Exception dédiée aux erreurs d'une étape du pipeline.
    /// Permet d'identifier rapidement l'agent responsable d'un échec.
    /// </summary>
    public sealed class PipelineStepException : Exception
    {
        /// <summary>
        /// Nom de l'étape ou de l'agent ayant échoué.
        /// </summary>
        public string StepName { get; }

        /// <summary>
        /// Crée une exception pour une étape donnée.
        /// </summary>
        public PipelineStepException(string stepName, string message)
            : base(message)
        {
            StepName = stepName;
        }

        /// <summary>
        /// Crée une exception pour une étape donnée avec exception interne.
        /// </summary>
        public PipelineStepException(string stepName, string message, Exception innerException)
            : base(message, innerException)
        {
            StepName = stepName;
        }
    }
}
