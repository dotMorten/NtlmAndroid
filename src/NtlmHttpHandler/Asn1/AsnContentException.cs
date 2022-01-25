// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;

namespace System.Formats.Asn1
{
    [Serializable]
    internal class AsnContentException : Exception
    {
        public AsnContentException()
            : base("The ASN.1 value is invalid.")
        {
        }

        public AsnContentException(string? message)
            : base(message ?? "The ASN.1 value is invalid.")
        {
        }

        public AsnContentException(string? message, Exception? inner)
            : base(message ?? "The ASN.1 value is invalid.", inner)
        {
        }

        protected AsnContentException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
