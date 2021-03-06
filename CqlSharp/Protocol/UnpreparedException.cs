// CqlSharp - CqlSharp
// Copyright (c) 2013 Joost Reuzel
//   
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//   
// http://www.apache.org/licenses/LICENSE-2.0
//  
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;

namespace CqlSharp.Protocol
{
    /// <summary>
    /// Can be thrown while a prepared statement tries to be executed with a prepared statement ID not known by the used host.
    /// </summary>
    [Serializable]
    public class UnpreparedException : ProtocolException
    {
        internal UnpreparedException(string message, byte[] unknownId, Guid? tracingId)
            : base(Protocol.ErrorCode.Unprepared, message, tracingId)
        {
            UnknownId = unknownId;
        }

        /// <summary>
        /// Gets the unknown unique identifier of the prepared statement.
        /// </summary>
        public byte[] UnknownId { get; private set; }
    }
}