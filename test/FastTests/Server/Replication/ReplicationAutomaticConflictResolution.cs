﻿// -----------------------------------------------------------------------
//  <copyright file="AutomaticConflictResolution.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using FastTests.Server.Basic.Entities;
using Raven.Client.Documents.Replication;
using Raven.Tests.Core.Utils.Entities;
using Xunit;

namespace FastTests.Server.Replication
{
    public class AutomaticConflictResolution : ReplicationTestsBase
    {
        [Fact]
        public void ShouldResolveDocumentConflictInFavorOfLatestVersion()
        {
            using (var master = GetDocumentStore())
            using (var slave = GetDocumentStore())
            {
                SetReplicationConflictResolution(slave, StraightforwardConflictResolution.ResolveToLatest);
                SetupReplication(master, slave);

                using (var session = slave.OpenSession())
                {
                
                    session.Store(new User()
                    {
                        Name = "1st"
                    }, "users/1");

                    session.SaveChanges();
                }

                using (var session = master.OpenSession())
                {
                    session.Store(new User()
                    {
                        Name = "1st"
                    }, "users/2");

                    session.Store(new
                    {
                        Foo = "marker"
                    }, "marker1");

                    session.SaveChanges();
                }

                Assert.True(WaitForDocument(slave, "marker1"));

                using (var session = slave.OpenSession())
                {
                    session.Store(new User()
                    {
                        Name = "2nd"
                    }, "users/2");

                    session.SaveChanges();
                }

                using (var session = master.OpenSession())
                {
                    session.Store(new User()
                    {
                        Name = "2nd"
                    }, "users/1");

                    session.Store(new
                    {
                        Foo = "marker"
                    }, "marker2");

                    session.SaveChanges();
                }

                Assert.True(WaitForDocument(slave, "marker2"));

                using (var session = slave.OpenSession())
                {
                    var user1 = session.Load<User>("users/1");
                    var user2 = session.Load<User>("users/2");

                    Assert.Equal("2nd", user1.Name);
                    Assert.Equal("2nd", user2.Name);
                }
            }
        }

        //resolve conflict between incoming document and tombstone
        [Fact]
        public void Resolve_to_latest_version_tombstone_is_latest_the_incoming_document_is_replicated()
        {
            using (var master = GetDocumentStore())
            using (var slave = GetDocumentStore())
            {
                using (var session = master.OpenSession())
                {
                    session.Store(new User()
                    {
                        Name = "1st"
                    }, "users/1");

                    session.SaveChanges();
                }

                using (var session = slave.OpenSession())
                {
                    session.Store(new User()
                    {
                        Name = "2nd"
                    }, "users/1");

                    session.SaveChanges();
                }

                using (var session = slave.OpenSession())
                {
                    session.Delete("users/1");
                    session.SaveChanges();
                }

                using (var session = master.OpenSession())
                {
                    session.Store(new
                    {
                        Foo = "marker"
                    }, "marker");

                    session.SaveChanges();
                }

                //the tombstone on the 'slave' node is latest, so after replication finishes,
                //the doc should stay deleted since the replication is 'resolve to latest'
                SetReplicationConflictResolution(slave, StraightforwardConflictResolution.ResolveToLatest);
                SetupReplication(master, slave);

                var marker = WaitForDocument(slave, "marker");

                Assert.NotNull(marker);

                using (var session = slave.OpenSession())
                {
                    var user = session.Load<User>("users/1");
                    Assert.Null(user);
                    //Assert.Equal("1st", user.Name);
                }
            }
        }
    }
}