using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chateau.Architecture
{
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public readonly struct ValidationMessage
    {
        public ValidationMessage(ValidationSeverity severity, string message, UnityEngine.Object context = null)
        {
            Severity = severity;
            Message = string.IsNullOrWhiteSpace(message) ? "Unspecified validation message." : message.Trim();
            Context = context;
        }

        public ValidationSeverity Severity { get; }
        public string Message { get; }
        public UnityEngine.Object Context { get; }

        public override string ToString()
        {
            return $"[{Severity}] {Message}";
        }
    }

    public sealed class ValidationReport
    {
        private readonly List<ValidationMessage> messages = new List<ValidationMessage>();

        public IReadOnlyList<ValidationMessage> Messages => messages;
        public bool HasErrors => messages.Exists(message => message.Severity == ValidationSeverity.Error);
        public int ErrorCount => messages.FindAll(message => message.Severity == ValidationSeverity.Error).Count;
        public int WarningCount => messages.FindAll(message => message.Severity == ValidationSeverity.Warning).Count;

        public void AddInfo(string message, UnityEngine.Object context = null)
        {
            messages.Add(new ValidationMessage(ValidationSeverity.Info, message, context));
        }

        public void AddWarning(string message, UnityEngine.Object context = null)
        {
            messages.Add(new ValidationMessage(ValidationSeverity.Warning, message, context));
        }

        public void AddError(string message, UnityEngine.Object context = null)
        {
            messages.Add(new ValidationMessage(ValidationSeverity.Error, message, context));
        }

        public void Append(ValidationReport other)
        {
            if (other == null)
            {
                return;
            }

            for (int i = 0; i < other.messages.Count; i++)
            {
                messages.Add(other.messages[i]);
            }
        }

        public void LogToUnity(string heading = "Chateau architecture validation")
        {
            if (!string.IsNullOrWhiteSpace(heading))
            {
                Debug.Log(heading);
            }

            for (int i = 0; i < messages.Count; i++)
            {
                ValidationMessage message = messages[i];

                switch (message.Severity)
                {
                    case ValidationSeverity.Error:
                        Debug.LogError(message.Message, message.Context);
                        break;
                    case ValidationSeverity.Warning:
                        Debug.LogWarning(message.Message, message.Context);
                        break;
                    default:
                        Debug.Log(message.Message, message.Context);
                        break;
                }
            }
        }
    }

    public interface IArchitectureValidatable
    {
        void ValidateConfiguration(ValidationReport report);
    }
}
