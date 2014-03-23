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
    /// Timeout exception during a read request
    /// </summary>
    [Serializable]
    public class ReadTimeOutException : TimeOutException
    {
        internal ReadTimeOutException(string message, CqlConsistency cqlConsistency, int received, int blockFor,
                                    bool dataPresent, Guid? tracingId)
            : base(Protocol.ErrorCode.ReadTimeout, message, cqlConsistency, received, blockFor, tracingId)
        {
            DataPresent = dataPresent;
        }

        public bool DataPresent { get; private set; }
    }
}