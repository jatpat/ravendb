﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using FastTests.Server.Basic.Entities;
using Raven.Client.Documents;
using Raven.Client.Exceptions;
using Raven.Tests.Core.Utils.Entities;
using Xunit;

namespace FastTests.Server.Replication
{
    public class ManualConflictResolution : ReplicationTestsBase
    {
        [Fact]
        public void CanManuallyResolveConflict()
        {
            using (var master = GetDocumentStore())
            using (var slave = GetDocumentStore())
            {

                SetScriptResolution(slave, "return {Name:docs[0].Name + '123'};","Users");
                SetupReplication(master, slave);

                using (var session = master.OpenSession())
                {
                    session.Store(new User()
                    {
                        Name = "Karmel"
                    }, "users/1");
                    session.SaveChanges();
                }

                var updated = WaitForDocument(slave, "users/1");
                Assert.True(updated);

                using (var session = slave.OpenSession())
                {
                    session.Delete("users/1");
                    session.SaveChanges();
                }
                using (var session = master.OpenSession())
                {
                    session.Store(new User()
                    {
                        Name = "Karmeli"
                    }, "users/1");
                    session.SaveChanges();
                }

                var updated2 = WaitForDocument(slave, "users/1");
                Assert.True(updated2);

                using (var session = slave.OpenSession())
                {
                    try
                    {
                        var item = session.Load<User>("users/1");
                        Assert.Equal(item.Name, "Karmeli123");
                    }
                    catch (ConflictException)
                    {
                        //Assert.Equal(HttpStatusCode.Conflict, e.StatusCode);
                        //var list = new List<JsonDocument>();
                        //for (int i = 0; i < e.ConflictedVersionIds.Length; i++)
                        //{
                        //	var doc = slave.DatabaseCommands.Get(e.ConflictedVersionIds[i]);
                        //	list.Add(doc);
                        //}

                        //var resolved = list[0];
                        ////TODO : when client API is finished, refactor this so the test works as designed
                        ////resolved.Metadata.Remove(Constants.RavenReplicationConflictDocument);
                        //slave.DatabaseCommands.Put("users/1", null, resolved.DataAsJson, resolved.Metadata);
                    }
                }
            }
        }


        [Fact]
        public void ScriptResolveToTombstone()
        {
            using (var master = GetDocumentStore())
            using (var slave = GetDocumentStore())
            {

                SetScriptResolution(slave, "return ResolveToTombstone();", "Users");
                SetupReplication(master, slave);

                using (var session = slave.OpenSession())
                {
                    session.Store(new User()
                    {
                        Name = "Karmel"
                    }, "users/1");
                    session.SaveChanges();
                }

                using (var session = master.OpenSession())
                {
                    session.Store(new User()
                    {
                        Name = "Karmeli"
                    }, "users/1");
                    session.SaveChanges();
                }

                var tombstoneIDs = WaitUntilHasTombstones(slave);
                Assert.Equal(1, tombstoneIDs.Count);
            }
        }

        [Fact]
        public void ScriptComplexResolution()
        {
            using (var master = GetDocumentStore())
            using (var slave = GetDocumentStore())
            {

                SetScriptResolution(slave, @"

function onlyUnique(value, index, self) { 
    return self.indexOf(value) === index;
}

    var names = [];
    var history = [];
    for(var i = 0; i < docs.length; i++) 
    {
        names = names.concat(docs[i].Name.split(' '));
        history.push(docs[i]);
    }
            var out = {
                Name: names.filter(onlyUnique).join(' '),
                Age: Math.max.apply(Math,docs.map(function(o){return o.Age;})),
                Grades:{Bio:12,Math:123,Pys:5,Sports:44},
                Versions:history,
                '@metadata':docs[0]['@metadata']
            }
output(out);
return out;
", "Users");
                SetupReplication(master, slave);
                long? etag;
                using (var session = slave.OpenSession())
                {
                    session.Store(new User()
                    {
                        Name = "Karmel",
                        Age = 12
                    }, "users/1");
                    session.SaveChanges();
                    etag = session.Advanced.GetEtagFor(session.Load<User>("users/1"));
                }

                using (var session = master.OpenSession())
                {
                    session.Store(new User()
                    {
                        Name = "Karmel",
                        Age = 123
                    }, "users/1");
                    session.SaveChanges();
                }


                var update = WaitForBiggerEtag(slave, etag);
                Assert.True(update);

                using (var session = slave.OpenSession())
                {
                    try
                    {
                        var item = session.Load<User>("users/1");
                        Assert.Equal("Karmel", item.Name);
                        Assert.Equal(123, item.Age);
                    }
                    catch (ConflictException)
                    {
                    }
                }
            }
        }

        [Fact]
        public void ScriptUnableToResolve()
        {
            using (var master = GetDocumentStore())
            using (var slave = GetDocumentStore())
            {
                SetupReplication(master, slave);
                SetScriptResolution(slave, @"return;", "Users");

                long? etag;
                using (var session = slave.OpenSession())
                {
                    session.Store(new User()
                    {
                        Name = "Karmel1",
                        Age = 1
                    }, "users/1");
                    session.SaveChanges();
                    etag = session.Advanced.GetEtagFor(session.Load<User>("users/1"));
                }

                using (var session = master.OpenSession())
                {
                    session.Store(new User()
                    {
                        Name = "Karmel2",
                        Age = 2
                    }, "users/1");
                    session.SaveChanges();
                }

                var conflicts = WaitUntilHasConflict(slave, "users/1");
                Assert.Equal(2, conflicts["users/1"].Count);
            }
        }

        public bool WaitForBiggerEtag(DocumentStore store, long? etag)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 10000)
            {
                using (var session = store.OpenSession())
                {
                    var doc = session.Load<User>("users/1");
                    if (session.Advanced.GetEtagFor(doc) > etag)
                        return true;
                }
                Thread.Sleep(10);
            }
            return false;
        }

        public bool WaitForResolution(DocumentStore store)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 10000)
            {
                using (var session = store.OpenSession())
                {
                    try
                    {
                        session.Load<User>("users/1");
                        return true;
                    }
                    catch
                    {
                        // ignored
                    }
                }
                Thread.Sleep(100);
            }
            return false;
        }
    }
}