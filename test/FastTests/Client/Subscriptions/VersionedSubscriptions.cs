﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Session;
using Raven.Client.Documents.Subscriptions;
using Raven.Client.Server.Versioning;
using Raven.Tests.Core.Utils.Entities;
using Sparrow.Json;
using Xunit;
using Sparrow;

namespace FastTests.Client.Subscriptions
{
    public class VersionedSubscriptions:RavenTestBase
    {
        private readonly TimeSpan _reasonableWaitTime = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(15);
        [Fact]
        public async Task PlainVersionedSubscriptions()
        {
            using (var store = GetDocumentStore())
            {

                var subscriptionId = await store.Subscriptions.CreateAsync(new SubscriptionCreationOptions<Versioned<User>>());

                using (var context = JsonOperationContext.ShortTermSingleUse())
                {
                    var versioningDoc = new VersioningConfiguration
                    {
                        Default = new VersioningCollectionConfiguration
                        {
                            Active = true,
                            MinimumRevisionsToKeep = 5,
                        },
                        Collections = new Dictionary<string, VersioningCollectionConfiguration>
                        {
                            ["Users"] = new VersioningCollectionConfiguration
                            {
                                Active = true
                            },
                            ["Dons"] = new VersioningCollectionConfiguration
                            {
                                Active = true,
                            }
                        }
                    };

                    await Server.ServerStore.ModifyDatabaseVersioning(context,
                        store.Database,
                        EntityToBlittable.ConvertEntityToBlittable(versioningDoc,
                            new DocumentConventions(),
                            context));
                }

                for (int i = 0; i < 10; i++)
                {
                    for (var j = 0; j < 10; j++)
                    {
                        using (var session = store.OpenSession())
                        {
                            session.Store(new User
                            {
                                Name = $"users{i} ver {j}"
                            }, "users/" + i);

                            session.Store(new Company()
                            {
                                Name = $"dons{i} ver {j}"
                            }, "dons/" + i);

                            session.SaveChanges();
                        }
                    }
                }

                using (var sub = store.Subscriptions.Open<Versioned<User>>(new SubscriptionConnectionOptions(subscriptionId)))
                {
                    var mre = new AsyncManualResetEvent();
                    var names = new HashSet<string>();
                    GC.KeepAlive(sub.Run(x =>
                    {
                        foreach (var item in x.Items)
                        {
                            names.Add(item.Result.Current?.Name + item.Result.Previous?.Name);

                            if (names.Count == 100)
                                mre.Set();
                        }
                    }));

                    Assert.True(await mre.WaitAsync(_reasonableWaitTime));

                }
            }
        }

        [Fact]
        public async Task PlainVersionedSubscriptionsCompareDocs()
        {
            using (var store = GetDocumentStore())
            {

                var subscriptionId = await store.Subscriptions.CreateAsync(new SubscriptionCreationOptions<Versioned<User>>());

                using (var context = JsonOperationContext.ShortTermSingleUse())
                {
                    var versioningDoc = new VersioningConfiguration
                    {
                        Default = new VersioningCollectionConfiguration
                        {
                            Active = true,
                            MinimumRevisionsToKeep = 5,
                        },
                        Collections = new Dictionary<string, VersioningCollectionConfiguration>
                        {
                            ["Users"] = new VersioningCollectionConfiguration
                            {
                                Active = true
                            },
                            ["Dons"] = new VersioningCollectionConfiguration
                            {
                                Active = true,
                            }
                        }
                    };

                    await Server.ServerStore.ModifyDatabaseVersioning(context,
                        store.Database,
                        EntityToBlittable.ConvertEntityToBlittable(versioningDoc,
                            new DocumentConventions(),
                            context));
                }

                for (var j = 0; j < 10; j++)
                {
                    using (var session = store.OpenSession())
                    {
                        session.Store(new User
                        {
                            Name = $"users1 ver {j}",
                            Age = j
                        }, "users/1");

                        session.Store(new Company()
                        {
                            Name = $"dons1 ver {j}"
                        }, "dons/1");

                        session.SaveChanges();
                    }
                }

                using (var sub = store.Subscriptions.Open<Versioned<User>>(new SubscriptionConnectionOptions(subscriptionId)))
                {
                    var mre = new AsyncManualResetEvent();
                    var names = new HashSet<string>();
                    var maxAge = -1;
                    GC.KeepAlive(sub.Run(a =>
                    {
                        foreach (var item in a.Items)
                        {
                            var x = item.Result;
                            if (x.Current.Age > maxAge && x.Current.Age > (x.Previous?.Age ?? -1))
                            {
                                names.Add(x.Current?.Name + x.Previous?.Name);
                                maxAge = x.Current.Age;
                            }
                            names.Add(x.Current?.Name + x.Previous?.Name);
                            if (names.Count == 10)
                                mre.Set();
                        }
                    }));

                    Assert.True(await mre.WaitAsync(_reasonableWaitTime));

                }
            }
        }

        public class Result
        {
            public string Id;
            public int Age;
        }

        [Fact]
        public async Task VersionedSubscriptionsWithCustomScript()
        {
            using (var store = GetDocumentStore())
            {

                var subscriptionId = await store.Subscriptions.CreateAsync(new SubscriptionCreationOptions<User>
                {
                    Criteria = new SubscriptionCriteria<User>
                    {
                        Script = @"
                        if(!!this.Current && !!this.Previous && this.Current.Age > this.Previous.Age)
                        {
                            return { Id: this.Current[""@metadata""][""@id""], Age: this.Current.Age }
                        }
                        else return false;
                        ",
                        IsVersioned = true
                    }
                });

                using (var context = JsonOperationContext.ShortTermSingleUse())
                {
                    var versioningDoc = new VersioningConfiguration
                    {
                        Default = new VersioningCollectionConfiguration
                        {
                            Active = true,
                            MinimumRevisionsToKeep = 5,
                        },
                        Collections = new Dictionary<string, VersioningCollectionConfiguration>
                        {
                            ["Users"] = new VersioningCollectionConfiguration
                            {
                                Active = true
                            },
                            ["Dons"] = new VersioningCollectionConfiguration
                            {
                                Active = true,
                            }
                        }
                    };

                    await Server.ServerStore.ModifyDatabaseVersioning(context,
                        store.Database,
                        EntityToBlittable.ConvertEntityToBlittable(versioningDoc,
                            new DocumentConventions(),
                            context));
                }

                for (int i = 0; i < 10; i++)
                {
                    for (var j = 0; j < 10; j++)
                    {
                        using (var session = store.OpenSession())
                        {
                            session.Store(new User
                            {
                                Name = $"users{i} ver {j}",
                                Age=j
                            }, "users/" + i);

                            session.Store(new Company()
                            {
                                Name = $"dons{i} ver {j}"
                            }, "companies/" + i);

                            session.SaveChanges();
                        }
                    }
                }

                using (var sub = store.Subscriptions.Open<Result>(new SubscriptionConnectionOptions(subscriptionId)))
                {
                    var mre = new AsyncManualResetEvent();
                    var names = new HashSet<string>();
                    GC.KeepAlive(sub.Run(x =>
                    {
                        foreach (var item in x.Items)
                        {
                            names.Add(item.Result.Id + item.Result.Age);
                            if (names.Count == 90)
                                mre.Set();
                        }
                    }));

                    Assert.True(await mre.WaitAsync(_reasonableWaitTime));

                }
            }
        }

        [Fact]
        public async Task VersionedSubscriptionsWithCustomScriptCompareDocs()
        {
            using (var store = GetDocumentStore())
            {

                var subscriptionId = await store.Subscriptions.CreateAsync(new SubscriptionCreationOptions<User>
                {
                    Criteria = new SubscriptionCriteria<User>
                    {
                        Script = @"
                        if(!!this.Current && !!this.Previous && this.Current.Age > this.Previous.Age)
                        {
                            return { Id: this.Current[""@metadata""][""@id""], Age: this.Current.Age }
                        }
                        else return false;
                        ",
                        IsVersioned = true

                    }
                });

                using (var context = JsonOperationContext.ShortTermSingleUse())
                {
                    var versioningDoc = new VersioningConfiguration
                    {
                        Default = new VersioningCollectionConfiguration
                        {
                            Active = true,
                            MinimumRevisionsToKeep = 5,
                        },
                        Collections = new Dictionary<string, VersioningCollectionConfiguration>
                        {
                            ["Users"] = new VersioningCollectionConfiguration
                            {
                                Active = true
                            },
                            ["Dons"] = new VersioningCollectionConfiguration
                            {
                                Active = true,
                            }
                        }
                    };

                    await Server.ServerStore.ModifyDatabaseVersioning(context,
                        store.Database,
                        EntityToBlittable.ConvertEntityToBlittable(versioningDoc,
                            new DocumentConventions(),
                            context));
                }

                for (int i = 0; i < 10; i++)
                {
                    for (var j = 0; j < 10; j++)
                    {
                        using (var session = store.OpenSession())
                        {
                            session.Store(new User
                            {
                                Name = $"users{i} ver {j}",
                                Age = j
                            }, "users/" + i);

                            session.Store(new Company()
                            {
                                Name = $"dons{i} ver {j}"
                            }, "companies/" + i);

                            session.SaveChanges();
                        }
                    }
                }

                using (var sub = store.Subscriptions.Open<Result>(new SubscriptionConnectionOptions(subscriptionId)))
                {
                    var mre = new AsyncManualResetEvent();
                    var names = new HashSet<string>();
                    var maxAge = -1;
                    GC.KeepAlive(sub.Run(x =>
                    {
                        foreach (var item in x.Items)
                        {
                            if (item.Result.Age > maxAge)
                            {
                                names.Add(item.Result.Id + item.Result.Age);
                                maxAge = item.Result.Age;
                            }

                            if (names.Count == 9)
                                mre.Set();
                        }
                    }));

                    Assert.True(await mre.WaitAsync(_reasonableWaitTime));

                }
            }
        }
    }
}