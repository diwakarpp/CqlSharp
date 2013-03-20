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

using CqlSharp.Config;
using CqlSharp.Network.Partition;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CqlSharp.Network
{
    /// <summary>
    ///   This implementation attempts to balance the connections over the cluster based on load. First it will
    ///   try to reuse an existing connection. If no connections exist, or if all connection loads are larger than
    ///   the newConnectionTreshold, a new connection is created at a node with an as low as possible load. If that fails
    ///   (e.g. because the max amount of connections per node is reached), an attempt is made to select the least used
    ///   connection from the least used node.
    /// </summary>
    internal class BalancedConnectionStrategy : IConnectionStrategy
    {
        private readonly ClusterConfig _config;
        private readonly Ring _nodes;

        /// <summary>
        ///   Initializes the strategy with the specified nodes and cluster configuration
        /// </summary>
        /// <param name="nodes"> The nodes. </param>
        /// <param name="config"> The config. </param>
        public BalancedConnectionStrategy(Ring nodes, ClusterConfig config)
        {
            _nodes = nodes;
            _config = config;
        }

        #region IConnectionStrategy Members

        /// <summary>
        ///   Gets or creates connection to the cluster.
        /// </summary>
        /// <param name="partitionKey"> </param>
        /// <returns> </returns>
        /// <exception cref="CqlException">Can not connect to any node of the cluster! All connectivity to the cluster seems to be lost</exception>
        public async Task<Connection> GetOrCreateConnectionAsync(PartitionKey partitionKey)
        {
            //Sort the nodes by load (used first)
            var nodesByLoad = new List<Node>(_nodes.Where(n => n.IsUp).OrderBy(n => n.ConnectionCount > 0 ? n.Load : int.MaxValue));

            int index = 0;

            //find the least used connection per node and see if it can be used
            for (; index < nodesByLoad.Count; index++)
            {
                Connection connection = nodesByLoad[index].GetConnection();

                //break when no connection found (nodes with connections come first, thus if no connection available, no more to be expected...)
                if (connection == null)
                    break;

                if (connection.Load < _config.NewConnectionTreshold)
                    return connection;
            }

            //check if we may create another connection
            if (_config.MaxConnections <= 0 || nodesByLoad.Sum(n => n.ConnectionCount) < _config.MaxConnections)
            {
                //iterate over the remaining (and initial) nodes, and try to create a connection
                for (int i = 0; i < nodesByLoad.Count; i++)
                {
                    try
                    {
                        Node node = nodesByLoad[(index + i) % nodesByLoad.Count];
                        Connection connection = await node.CreateConnectionAsync();

                        if (connection != null)
                            return connection;
                    }
                    // ReSharper disable EmptyGeneralCatchClause
                    catch
                    {
                        //ignore, errors handled within node, try to create another one at the next node
                    }
                    // ReSharper restore EmptyGeneralCatchClause
                }
            }

            //no suitable connection found or created, go to the least used node, and pick its least used open connection...
            foreach (Node node in nodesByLoad)
            {
                Connection connection = node.GetConnection();
                if (connection != null)
                    return connection;
            }

            return null;
        }


        /// <summary>
        ///   Invoked when a connection is no longer in use by the application
        /// </summary>
        /// <param name="connection"> The connection no longer used. </param>
        public void ReturnConnection(Connection connection)
        {
            //connections are shared, nothing to do here
        }

        #endregion
    }
}