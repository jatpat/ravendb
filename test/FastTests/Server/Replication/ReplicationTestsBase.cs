﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Raven.NewClient.Abstractions.Data;
using Raven.NewClient.Client.Commands;
using Raven.NewClient.Client.Document;
using Raven.NewClient.Client.Exceptions;
using Raven.NewClient.Client.Http;
using Raven.NewClient.Client.Json;
using Raven.NewClient.Client.Replication;
using Raven.NewClient.Client.Replication.Messages;
using Sparrow.Json;
using Xunit;

namespace FastTests.Server.Replication
{
    public class ReplicationTestsBase : RavenNewTestBase
    {
        protected Dictionary<string, object> GetReplicationStats(DocumentStore store)
        {
            using (var commands = store.Commands())
            {
                var command = new GetReplicationStatsCommand();

                commands.RequestExecuter.Execute(command, commands.Context);

                return command.Result;
            }
        }

        protected void EnsureReplicating(DocumentStore src, DocumentStore dst)
        {
            var id = "marker/" + Guid.NewGuid().ToString();
            using (var s = src.OpenSession())
            {
                s.Store(new { }, id);
                s.SaveChanges();
            }
            WaitForDocumentToReplicate<object>(dst, id, 15 * 1000);
        }


        protected Dictionary<string,string[]> GetConnectionFaliures(DocumentStore store)
        {
            using (var commands = store.Commands())
            {
                var command = new GetConncectionFailuresCommand();

                commands.RequestExecuter.Execute(command, commands.Context);

                return command.Result;
            }
        }

        protected Dictionary<string, List<ChangeVectorEntry[]>> GetConflicts(DocumentStore store, string docId)
        {
            using (var commands = store.Commands())
            {
                var command = new GetReplicationConflictsCommand(docId);

                commands.RequestExecuter.Execute(command, commands.Context);

                return command.Result;
            }
        }

        protected Dictionary<string, List<ChangeVectorEntry[]>> WaitUntilHasConflict(
                DocumentStore store,
                string docId,
                int count = 2)
        {
            int timeout = 5000;

            if (Debugger.IsAttached)
                timeout *= 100;
            Dictionary<string, List<ChangeVectorEntry[]>> conflicts;
            var sw = Stopwatch.StartNew();
            do
            {
                conflicts = GetConflicts(store, docId);

                List<ChangeVectorEntry[]> list;
                if (conflicts.TryGetValue(docId, out list) == false)
                    list = new List<ChangeVectorEntry[]>();
                if (list.Count >= count)
                    break;

                if (sw.ElapsedMilliseconds > timeout)
                {
                    Assert.False(true,
                        "Timed out while waiting for conflicts on " + docId + " we have " + list.Count + " conflicts");
                }
            } while (true);
            return conflicts;
        }

        protected bool WaitForDocumentDeletion(DocumentStore store,
            string docId,
            int timeout = 10000)
        {
            if (Debugger.IsAttached)
                timeout *= 100;

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeout)
            {
                using (var session = store.OpenSession())
                {
                    try
                    {
                        var doc = session.Load<dynamic>(docId);
                        if (doc == null)
                            return true;
                    }
                    catch (ConflictException)
                    {
                        // expected that we might get conflict, ignore and wait
                    }
                }
            }
            using (var session = store.OpenSession())
            {
                //one last try, and throw if there is still a conflict
                var doc = session.Load<dynamic>(docId);
                if (doc == null)
                    return true;
            }
            return false;
        }

        protected bool WaitForDocument<T>(DocumentStore store,
            string docId,
            Func<T, bool> predicate, 
            int timeout = 10000)
        {
            if (Debugger.IsAttached)
                timeout *= 100;

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeout)
            {
                using (var session = store.OpenSession())
                {
                    try
                    {
                        var doc = session.Load<T>(docId);
                        if (doc != null && predicate(doc))
                            return true;
                    }
                    catch (ConflictException)
                    {
                        // expected that we might get conflict, ignore and wait
                    }
                }
            }
            using (var session = store.OpenSession())
            {
                //one last try, and throw if there is still a conflict
                var doc = session.Load<T>(docId);
                if (doc != null && predicate(doc))
                    return true;
            }
            return false;
        }

        protected bool WaitForDocument(DocumentStore store,
            string docId,
            int timeout = 10000)
        {
            if (Debugger.IsAttached)
                timeout *= 100;

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeout)
            {
                using (var session = store.OpenSession())
                {
                    try
                    {
                        var doc = session.Load<dynamic>(docId);
                        if (doc != null)
                            return true;
                    }
                    catch (ConflictException)
                    {
                        // expected that we might get conflict, ignore and wait
                    }
                }
            }
            using (var session = store.OpenSession())
            {
                //one last try, and throw if there is still a conflict
                var doc = session.Load<dynamic>(docId);
                if (doc != null)
                    return true;
            }
            return false;
        }

        protected T WaitForValue<T>(Func<T> act, T expectedVal)
        {
            int timeout = 5000;
            if (Debugger.IsAttached)
                timeout *= 100;
            var sw = Stopwatch.StartNew();
            do
            {
                var currentVal = act();
                if (expectedVal.Equals(currentVal))
                {
                    return currentVal;
                }
                if (sw.ElapsedMilliseconds > timeout)
                {
                    return currentVal;
                }
                Thread.Sleep(16);
            } while (true);
        }

        protected List<string> WaitUntilHasTombstones(
                DocumentStore store,
                int count = 1)
        {

            int timeout = 5000;
            if (Debugger.IsAttached)
                timeout *= 100;
            List<string> tombstones;
            var sw = Stopwatch.StartNew();
            do
            {
                tombstones = GetTombstones(store);

                if (tombstones == null ||
                    tombstones.Count >= count)
                    break;

                if (sw.ElapsedMilliseconds > timeout)
                {
                    Assert.False(true, store.Identifier + " -> Timed out while waiting for tombstones, we have " + tombstones.Count + " tombstones, but should have " + count);
                }

            } while (true);
            return tombstones ?? new List<string>();
        }


        protected List<string> GetTombstones(DocumentStore store)
        {
            using (var commands = store.Commands())
            {
                var command = new GetReplicationTombstonesCommand();

                commands.RequestExecuter.Execute(command, commands.Context);

                return command.Result;
            }
        }

        protected FullTopologyInfo GetFullTopology(DocumentStore store)
        {
            using (var commands = store.Commands())
            {
                var command = new GetFullTopologyCommand();

                commands.RequestExecuter.Execute(command, commands.Context);

                return command.Result;
            }
        }

        protected T WaitForDocumentToReplicate<T>(DocumentStore store, string id, int timeout)
            where T : class
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds <= timeout)
            {
                using (var session = store.OpenSession(store.DefaultDatabase))
                {
                    var doc = session.Load<T>(id);
                    if (doc != null)
                        return doc;
                }
            }

            return default(T);
        }

        public static void SetScriptResolution(DocumentStore store, string script, string collection)
        {
            using (var session = store.OpenSession())
            {
                session.Store(new ReplicationDocument
                {
                    DocumentConflictResolution = StraightforwardConflictResolution.None,
                    ResolveByCollection = new Dictionary<string, ScriptResolver>{
                            { collection, new ScriptResolver
                                {
                                    Script = script
                                }
                            }
                        }
                }, Constants.Replication.DocumentReplicationConfiguration);

                session.SaveChanges();
            }
        }

        protected static void SetReplicationConflictResolution(DocumentStore store,
            StraightforwardConflictResolution conflictResolution)
        {
            using (var session = store.OpenSession())
            {
                var destinations = new List<ReplicationDestination>();
                session.Store(new ReplicationDocument
                {
                    Destinations = destinations,
                    DocumentConflictResolution = conflictResolution
                }, Constants.Replication.DocumentReplicationConfiguration);
                session.SaveChanges();
            }
        }

        protected static void SetupReplication(DocumentStore fromStore, StraightforwardConflictResolution builtinConflictResolution = StraightforwardConflictResolution.None, params DocumentStore[] toStores)
        {
            using (var session = fromStore.OpenSession())
            {
                var destinations = new List<ReplicationDestination>();
                foreach (var store in toStores)
                    destinations.Add(
                        new ReplicationDestination
                        {
                            Database = store.DefaultDatabase,
                            Url = store.Url,

                        });
                session.Store(new ReplicationDocument
                {
                    Destinations = destinations,
                    DocumentConflictResolution = builtinConflictResolution
                }, Constants.Replication.DocumentReplicationConfiguration);
                session.SaveChanges();
            }
        }

        protected static void SetupReplication(DocumentStore fromStore, Dictionary<string,string> etlScripts, params DocumentStore[] toStores)
        {
            using (var session = fromStore.OpenSession())
            {
                var destinations = new List<ReplicationDestination>();
                foreach (var store in toStores)
                    destinations.Add(
                        new ReplicationDestination
                        {
                            Database = store.DefaultDatabase,
                            Url = store.Url,
                            SpecifiedCollections = etlScripts
                        });
                session.Store(new ReplicationDocument
                {
                    Destinations = destinations
                }, Constants.Replication.DocumentReplicationConfiguration);
                session.SaveChanges();
            }
        }


        protected void SetupReplication(DocumentStore fromStore, params DocumentStore[] toStores)
        {
            SetupReplication(fromStore, 
                new ReplicationDocument
                {
                    
                }, 
                toStores);
        }

        protected void SetupReplication(DocumentStore fromStore, ReplicationDocument configOptions, params DocumentStore[] toStores)
        {
            using (var session = fromStore.OpenSession())
            {
                var destinations = new List<ReplicationDestination>();
                foreach (var store in toStores)
                {
                    var replicationDestination = new ReplicationDestination
                    {
                        Database = store.DefaultDatabase,
                        Url = store.Url
                    };
                    ModifyReplicationDestination(replicationDestination);
                    destinations.Add(replicationDestination);
                }

                configOptions.Destinations = destinations;
                session.Store(configOptions, Constants.Replication.DocumentReplicationConfiguration);
                session.SaveChanges();
            }
        }

        protected virtual void ModifyReplicationDestination(ReplicationDestination replicationDestination)
        {
        }

        protected static void SetupReplicationWithCustomDestinations(DocumentStore fromStore, params ReplicationDestination[] toDestinations)
        {
            using (var session = fromStore.OpenSession())
            {
                session.Store(new ReplicationDocument
                {
                    Destinations = toDestinations.ToList()
                }, Constants.Replication.DocumentReplicationConfiguration);
                session.SaveChanges();
            }
        }


        private class GetConncectionFailuresCommand : RavenCommand<Dictionary<string, string[]>>
        {

            public override bool IsReadRequest => true;
            public override HttpRequestMessage CreateRequest(ServerNode node, out string url)
            {
                url = $"{node.Url}/databases/{node.Database}/replication/debug/incoming-rejection-info";

                ResponseType = RavenCommandResponseType.Array;
                return new HttpRequestMessage
                {
                    Method = HttpMethod.Get
                };
            }
            public override void SetResponse(BlittableJsonReaderArray response, bool fromCache)
            {
                List<string> list = new List<string>();
                Dictionary<string,string[]> result = new Dictionary<string, string[]>();
                foreach (BlittableJsonReaderObject responseItem in response.Items)
                {
                    BlittableJsonReaderObject obj;
                    responseItem.TryGet("Key", out obj);
                    string name;
                    obj.TryGet("SourceDatabaseName", out name);

                    BlittableJsonReaderArray arr;
                    responseItem.TryGet("Value", out arr);
                    list.Clear();
                    foreach (BlittableJsonReaderObject arrItem in arr)
                    {
                        string reason;
                        arrItem.TryGet("Reason", out reason);
                        list.Add(reason);
                    }
                    result.Add(name,list.ToArray());
                }
                Result = result;
            }
            public override void SetResponse(BlittableJsonReaderObject response, bool fromCache)
            {
                throw new NotImplementedException();
            }
        }

        private class GetFullTopologyCommand : RavenCommand<FullTopologyInfo>
        {
            public override bool IsReadRequest => true;

            public override HttpRequestMessage CreateRequest(ServerNode node, out string url)
            {
                url = $"{node.Url}/databases/{node.Database}/topology/full";

                return new HttpRequestMessage
                {
                    Method = HttpMethod.Get
                };
            }

            public override void SetResponse(BlittableJsonReaderObject response, bool fromCache)
            {
                if (response == null)
                    ThrowInvalidResponse();

                Result = JsonDeserializationClient.FullTopologyInfo(response);
            }
        }

        private class GetReplicationTombstonesCommand : RavenCommand<List<string>>
        {
            public override bool IsReadRequest => true;

            public override HttpRequestMessage CreateRequest(ServerNode node, out string url)
            {
                url = $"{node.Url}/databases/{node.Database}/replication/tombstones";

                return new HttpRequestMessage
                {
                    Method = HttpMethod.Get
                };
            }

            public override void SetResponse(BlittableJsonReaderObject response, bool fromCache)
            {
                if (response == null)
                    ThrowInvalidResponse();

                BlittableJsonReaderArray array;
                if (response.TryGet("Results", out array) == false)
                    ThrowInvalidResponse();

                var result = new List<string>();
                foreach (BlittableJsonReaderObject json in array)
                {
                    string key;
                    if (json.TryGet("Key", out key) == false)
                        ThrowInvalidResponse();

                    result.Add(key);
                }

                Result = result;
            }
        }

        private class GetReplicationStatsCommand : RavenCommand<Dictionary<string,object>>
        {
            public override bool IsReadRequest => true;
            public override HttpRequestMessage CreateRequest(ServerNode node, out string url)
            {
                url = $"{node.Url}/databases/{node.Database}/replication/stats";

                return new HttpRequestMessage
                {
                    Method = HttpMethod.Get
                };
            }

            public override void SetResponse(BlittableJsonReaderObject response, bool fromCache)
            {
                if (response == null)
                    ThrowInvalidResponse();

                BlittableJsonReaderObject liveStats;
                if (response.TryGet("LiveStats", out liveStats) == false)
                    ThrowInvalidResponse();

                DateTime sampledAt;
                long conflictsCount;
                string conflictResolverStatus;
                BlittableJsonReaderObject outgoingHeartbeats;
                BlittableJsonReaderObject incomingHeartbeats;
                var outgoingLastHeartbeat = new Dictionary<string, long>();
                var incomingLastHeartbeat = new Dictionary<string, long>();

                var liveStatsList = new List<object>();
                if (liveStats.TryGet("SampledAt", out sampledAt) &&
                    liveStats.TryGet("OutgoingHeartbeats", out outgoingHeartbeats) &&
                    liveStats.TryGet("IncomingHeartbeats", out incomingHeartbeats) &&
                    liveStats.TryGet("ConflictResolverStatus", out conflictResolverStatus) &&
                    liveStats.TryGet("ConflictsCount", out conflictsCount))
                {
                    
                    for (var i = 0; i < outgoingHeartbeats.Count; i++)
                    {
                        var prop = new BlittableJsonReaderObject.PropertyDetails();
                        outgoingHeartbeats.GetPropertyByIndex(i, ref prop);
                        outgoingLastHeartbeat[prop.Name] = (long) prop.Value;
                    }
                   for (var i = 0; i < incomingHeartbeats.Count; i++)
                    {
                        var prop = new BlittableJsonReaderObject.PropertyDetails();
                        incomingHeartbeats.GetPropertyByIndex(i, ref prop);
                        incomingLastHeartbeat[prop.Name] = (long)prop.Value;
                    }
                    liveStatsList.Add(sampledAt);
                    liveStatsList.Add(outgoingLastHeartbeat);
                    liveStatsList.Add(incomingLastHeartbeat);
                    liveStatsList.Add(conflictResolverStatus);
                    liveStatsList.Add(conflictsCount);
                } else
                    ThrowInvalidResponse();

                
                var incoming = new List<Dictionary<string, object>>();
                BlittableJsonReaderArray incomingStats;
                if (response.TryGet("IncomingStats", out incomingStats) == false)
                    ThrowInvalidResponse();
                
                var outgoing = new List<Dictionary<string, object>>();
                BlittableJsonReaderArray outgoingStats;
                if (response.TryGet("OutgoingStats", out outgoingStats) == false)
                    ThrowInvalidResponse();
                
                var resolver = new List<Dictionary<string, object>>();
                BlittableJsonReaderArray resovlerStats;
                if (response.TryGet("ResovlerStats", out resovlerStats) == false)
                    ThrowInvalidResponse();

                foreach (BlittableJsonReaderObject incomingStat in incomingStats)
                {
                    var entry = new Dictionary<string, object>();
                    for (var i = 0; i < incomingStat.Count; i++)
                    {
                        var prop = new BlittableJsonReaderObject.PropertyDetails();
                        incomingStat.GetPropertyByIndex(i, ref prop);
                        entry[prop.Name] = prop.Value;
                    }
                    incoming.Add(entry);
                }

                foreach (BlittableJsonReaderObject outgoingStat in outgoingStats)
                {
                    var entry = new Dictionary<string, object>();
                    for (var i = 0; i < outgoingStat.Count; i++)
                    {
                        var prop = new BlittableJsonReaderObject.PropertyDetails();
                        outgoingStat.GetPropertyByIndex(i, ref prop);
                        entry[prop.Name] = prop.Value;
                    }
                    outgoing.Add(entry);
                }

                foreach (BlittableJsonReaderObject resovlerStat in resovlerStats)
                {
                    var entry = new Dictionary<string, object>();
                    for (var i = 0; i < resovlerStat.Count; i++)
                    {
                        var prop = new BlittableJsonReaderObject.PropertyDetails();
                        resovlerStat.GetPropertyByIndex(i, ref prop);
                        entry[prop.Name] = prop.Value;
                    }
                    resolver.Add(entry);
                }
                Result = new Dictionary<string, object>
                {
                    {"LiveStat", liveStatsList } ,
                    {"IncomingStats", incoming},
                    {"OutgoingStats",outgoing},
                    {"ResolverStats",resolver}
                };
            }
        }

        private class GetReplicationConflictsCommand : RavenCommand<Dictionary<string, List<ChangeVectorEntry[]>>>
        {
            private readonly string _id;

            public GetReplicationConflictsCommand(string id)
            {
                if (id == null)
                    throw new ArgumentNullException(nameof(id));

                _id = id;
            }

            public override bool IsReadRequest => true;

            public override HttpRequestMessage CreateRequest(ServerNode node, out string url)
            {
                url = $"{node.Url}/databases/{node.Database}/replication/conflicts?docId={_id}";

                return new HttpRequestMessage
                {
                    Method = HttpMethod.Get
                };
            }

            public override void SetResponse(BlittableJsonReaderObject response, bool fromCache)
            {
                if (response == null)
                    ThrowInvalidResponse();

                BlittableJsonReaderArray array;
                if (response.TryGet("Results", out array) == false)
                    ThrowInvalidResponse();

                var result = new Dictionary<string, List<ChangeVectorEntry[]>>();
                foreach (BlittableJsonReaderObject json in array)
                {
                    string key;
                    if (json.TryGet("Key", out key) == false)
                        ThrowInvalidResponse();

                    BlittableJsonReaderArray vectorsArray;
                    if (json.TryGet("ChangeVector", out vectorsArray) == false)
                        ThrowInvalidResponse();

                    var vectors = new ChangeVectorEntry[vectorsArray.Length];
                    for (int i = 0; i < vectorsArray.Length; i++)
                    {
                        var vectorJson = (BlittableJsonReaderObject)vectorsArray[i];
                        var vector = new ChangeVectorEntry();

                        if (vectorJson.TryGet(nameof(ChangeVectorEntry.DbId), out vector.DbId) == false)
                            ThrowInvalidResponse();

                        if (vectorJson.TryGet(nameof(ChangeVectorEntry.Etag), out vector.Etag) == false)
                            ThrowInvalidResponse();

                        vectors[i] = vector;
                    }

                    List<ChangeVectorEntry[]> values;
                    if (result.TryGetValue(key, out values) == false)
                        result[key] = values = new List<ChangeVectorEntry[]>();

                    values.Add(vectors);
                }

                Result = result;
            }
        }
    }
}