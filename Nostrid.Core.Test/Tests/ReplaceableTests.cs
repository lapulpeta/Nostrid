using NNostr.Client;
using Nostrid.Data;
using Nostrid.Model;

namespace Nostrid.Core.Test
{
	[TestClass]
	public class ReplaceableTests
	{
		[TestMethod]
		public void TestReplaceableId()
		{
			var evjson = @"
            {
              ""id"": ""fda4cc3dd533b2a444a3373d38d4b69dcace17f3377d8573e48f16a37f9daa57"",
              ""pubkey"": ""3b8bd779b4b0a19e85babc3444a6ebd432e721de1e59534e8c9b71bc4f33ead2"",
              ""created_at"": 1677857980,
              ""kind"": 30023,
              ""tags"": [
                [
                  ""client"",
                  ""blogstack.io""
                ],
                [
                  ""title"",
                  ""Hola blogstack""
                ],
                [
                  ""d"",
                  ""e4826342""
                ],
                [
                  ""published_at"",
                  ""1677857980""
                ]
              ],
              ""content"": ""mi primer post en blogstack.io \nEsto es asombroso."",
              ""sig"": ""2ff8f8391130d1005620b5073f4403d517d9a9b1b59b01fd2ad3bb05037282ff1ef76f9ac4de26edf8cbff2f98d15a68a3068e1f599bb295aa38349c154e9897""
            }
            ";

			var uniqueId = EventExtension.GetReplaceableId(evjson.ToEvent());

			Assert.AreEqual("30023:3b8bd779b4b0a19e85babc3444a6ebd432e721de1e59534e8c9b71bc4f33ead2:e4826342", uniqueId);
		}

		[TestMethod]
		public void TestReplaceParams()
		{
			using var tempdb = new TempDb();

			var eventDatabase = new EventDatabase();
			eventDatabase.InitDatabase(tempdb.DbName);

			var relay = new Relay() { Uri = "xxxx" };

			eventDatabase.SaveRelay(relay);

			NostrEvent nostrev;
			Event ev;

			var pubkey = TestUtils.GetRandomNostrId();

			// Create first replaceable event

			nostrev = new NostrEvent()
			{
				Content = "Test",
				Kind = 33333,
				Tags = new()
				{
					new()
					{
						TagIdentifier = "d", Data = new(){ "testd" }
					}
				},
				CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
				PublicKey = pubkey,
			};
			nostrev.Id = nostrev.ComputeId();
			nostrev.Signature = TestUtils.GetRandomNostrId(); // Signature doesn't matter
			ev = nostrev.FromNostrEvent();
			var id = ev.Id;

			var replaceableId = ev.ReplaceableId;

			eventDatabase.SaveNewEvent(ev, relay);

			// It should be added

			{
				using var db = eventDatabase.CreateContext();
				Assert.AreEqual(1, db.Events.Count(e => e.ReplaceableId == replaceableId && e.Id == id));
			}

			// Create second replaceable event that should replace the previous one

			nostrev = new NostrEvent()
			{
				Content = "Test2",
				Kind = 33333,
				Tags = new()
				{
					new()
					{
						TagIdentifier = "d", Data = new(){ "testd" }
					}
				},
				CreatedAt = DateTimeOffset.UtcNow,
				PublicKey = pubkey,
			};
			nostrev.Id = nostrev.ComputeId();
			nostrev.Signature = TestUtils.GetRandomNostrId(); // Signature doesn't matter
			ev = nostrev.FromNostrEvent();
			var id2 = ev.Id;

			Assert.AreEqual(replaceableId, ev.ReplaceableId, "The replacing event should have the same replaceableId");

			// It should have replaced the first one

			eventDatabase.SaveNewEvent(ev, relay);

			{
				using var db = eventDatabase.CreateContext();
				Assert.AreEqual(0, db.Events.Count(e => e.ReplaceableId == replaceableId && e.Id == id));
				Assert.AreEqual(0, db.Events.Count(e => e.Id == id));
				Assert.AreEqual(1, db.Events.Count(e => e.ReplaceableId == replaceableId && e.Id == id2));
			}

			// Create a third event with date between first and second events

			nostrev = new NostrEvent()
			{
				Content = "Test3",
				Kind = 33333,
				Tags = new()
				{
					new()
					{
						TagIdentifier = "d", Data = new(){ "testd" }
					}
				},
				CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
				PublicKey = pubkey,
			};
			nostrev.Id = nostrev.ComputeId();
			nostrev.Signature = TestUtils.GetRandomNostrId(); // Signature doesn't matter
			ev = nostrev.FromNostrEvent();
			var id3 = ev.Id;

			Assert.AreEqual(replaceableId, ev.ReplaceableId, "The replacing event should have the same replaceableId");

			eventDatabase.SaveNewEvent(ev, relay);

			// Nothing should have changed

			{
				using var db = eventDatabase.CreateContext();
				Assert.AreEqual(0, db.Events.Count(e => e.ReplaceableId == replaceableId && e.Id == id));
				Assert.AreEqual(0, db.Events.Count(e => e.Id == id));
				Assert.AreEqual(1, db.Events.Count(e => e.ReplaceableId == replaceableId && e.Id == id2));
				Assert.AreEqual(0, db.Events.Count(e => e.Id == id3));
			}
		}

		[TestMethod]
		public void TestReplaceParams2()
		{
			using var tempdb = new TempDb();

			var eventDatabase = new EventDatabase();
			eventDatabase.InitDatabase(tempdb.DbName);

			var relay = new Relay() { Uri = "xxxx" };

			eventDatabase.SaveRelay(relay);

			Event ev;

			var pubkey = TestUtils.GetRandomNostrId();

			// Create base replaceable event

			Event CreateEvent(List<List<string>> tags)
			{
				var nev = new NostrEvent()
				{
					Content = Random.Shared.Next().ToString(),
					Kind = 33333,
					Tags = tags.Select(t => new NostrEventTag()
						{
							TagIdentifier = t[0], Data = t.Skip(1).ToList()
						}).ToList(),
					CreatedAt = DateTimeOffset.UtcNow,
					PublicKey = pubkey,
				};
				nev.Id = nev.ComputeId();
				nev.Signature = TestUtils.GetRandomNostrId(); // Signature doesn't matter
				return nev.FromNostrEvent();
			};

			//"tags":[["d",""]]
			ev = CreateEvent(new(){ new(){ "d", "" } });
			var replaceableId = ev.ReplaceableId;

			eventDatabase.SaveNewEvent(ev, relay);
			{
				using var db = eventDatabase.CreateContext();
				Assert.AreEqual(1, db.Events.Count());
				Assert.AreEqual(1, db.Events.Count(e => e.ReplaceableId == replaceableId && e.Id == ev.Id));
			}

			//"tags":[]: implicit d tag with empty value
			ev = CreateEvent(new() { });
			Assert.AreEqual(replaceableId, ev.ReplaceableId);

			eventDatabase.SaveNewEvent(ev, relay);
			{
				using var db = eventDatabase.CreateContext();
				Assert.AreEqual(1, db.Events.Count());
				Assert.AreEqual(1, db.Events.Count(e => e.ReplaceableId == replaceableId && e.Id == ev.Id));
			}

			//"tags":[["d"]]: implicit empty value ""
			ev = CreateEvent(new() { new() { "d" } });
			Assert.AreEqual(replaceableId, ev.ReplaceableId);

			eventDatabase.SaveNewEvent(ev, relay);
			{
				using var db = eventDatabase.CreateContext();
				Assert.AreEqual(1, db.Events.Count());
				Assert.AreEqual(1, db.Events.Count(e => e.ReplaceableId == replaceableId && e.Id == ev.Id));
			}

			//"tags":[["d",""],["d","not empty"]]: only first d tag is considered
			ev = CreateEvent(new() { new() { "d", "" }, new() { "d", "not empty" } });
			Assert.AreEqual(replaceableId, ev.ReplaceableId);

			eventDatabase.SaveNewEvent(ev, relay);
			{
				using var db = eventDatabase.CreateContext();
				Assert.AreEqual(1, db.Events.Count());
				Assert.AreEqual(1, db.Events.Count(e => e.ReplaceableId == replaceableId && e.Id == ev.Id));
			}

			//"tags":[["d"],["d","some value"]]: only first d tag is considered
			ev = CreateEvent(new() { new() { "d" }, new() { "d", "not empty" } });
			Assert.AreEqual(replaceableId, ev.ReplaceableId);

			eventDatabase.SaveNewEvent(ev, relay);
			{
				using var db = eventDatabase.CreateContext();
				Assert.AreEqual(1, db.Events.Count());
				Assert.AreEqual(1, db.Events.Count(e => e.ReplaceableId == replaceableId && e.Id == ev.Id));
			}

			//"tags":[["e"]]: same as no tags
			ev = CreateEvent(new() { new() { "e" } });
			Assert.AreEqual(replaceableId, ev.ReplaceableId);

			eventDatabase.SaveNewEvent(ev, relay);
			{
				using var db = eventDatabase.CreateContext();
				Assert.AreEqual(1, db.Events.Count());
				Assert.AreEqual(1, db.Events.Count(e => e.ReplaceableId == replaceableId && e.Id == ev.Id));
			}

			//"tags":[["d","","1"]]: only the value is considered
			ev = CreateEvent(new() { new() { "d", "", "1" } });
			Assert.AreEqual(replaceableId, ev.ReplaceableId);

			eventDatabase.SaveNewEvent(ev, relay);
			{
				using var db = eventDatabase.CreateContext();
				Assert.AreEqual(1, db.Events.Count());
				Assert.AreEqual(1, db.Events.Count(e => e.ReplaceableId == replaceableId && e.Id == ev.Id));
			}
		}

        [TestMethod]
        public void TestReplace()
        {
            using var tempdb = new TempDb();

            var eventDatabase = new EventDatabase();
            eventDatabase.InitDatabase(tempdb.DbName);

            var relay = new Relay() { Uri = "xxxx" };

            eventDatabase.SaveRelay(relay);

            NostrEvent nostrev;
            Event ev;

            var pubkey = TestUtils.GetRandomNostrId();

            // Create first replaceable event

            nostrev = new NostrEvent()
            {
                Content = "Test",
                Kind = 11111,
                Tags = new()
                {
                    new()
                    {
                        TagIdentifier = "d", Data = new(){ "testd" }
                    }
                },
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
                PublicKey = pubkey,
            };
            nostrev.Id = nostrev.ComputeId();
            nostrev.Signature = TestUtils.GetRandomNostrId(); // Signature doesn't matter
            ev = nostrev.FromNostrEvent();
            var id = ev.Id;

            var replaceableId = ev.ReplaceableId;

            eventDatabase.SaveNewEvent(ev, relay);

            // It should be added

            {
                using var db = eventDatabase.CreateContext();
                Assert.AreEqual(1, db.Events.Count(e => e.ReplaceableId == replaceableId && e.Id == id));
            }

            // Create second replaceable event that should replace the previous one

            nostrev = new NostrEvent()
            {
                Content = "Test2",
                Kind = 11111,
                Tags = new()
                {
                    new()
                    {
                        TagIdentifier = "d", Data = new(){ "testd2" } // Different d shouldn't matter
                    }
                },
                CreatedAt = DateTimeOffset.UtcNow,
                PublicKey = pubkey,
            };
            nostrev.Id = nostrev.ComputeId();
            nostrev.Signature = TestUtils.GetRandomNostrId(); // Signature doesn't matter
            ev = nostrev.FromNostrEvent();
            var id2 = ev.Id;

            Assert.AreEqual(replaceableId, ev.ReplaceableId, "The replacing event should have the same replaceableId");

            // It should have replaced the first one

            eventDatabase.SaveNewEvent(ev, relay);

            {
                using var db = eventDatabase.CreateContext();
                Assert.AreEqual(0, db.Events.Count(e => e.Id == id));
                Assert.AreEqual(1, db.Events.Count(e => e.ReplaceableId == replaceableId && e.Id == id2));
            }

            // Create a third event with date between first and second events

            nostrev = new NostrEvent()
            {
                Content = "Test3",
                Kind = 11111,
                Tags = new()
                {
                    new()
                    {
                        TagIdentifier = "d", Data = new(){ "testd2" } // Same d shouldn't matter
                    }
                },
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                PublicKey = pubkey,
            };
            nostrev.Id = nostrev.ComputeId();
            nostrev.Signature = TestUtils.GetRandomNostrId(); // Signature doesn't matter
            ev = nostrev.FromNostrEvent();
            var id3 = ev.Id;

            Assert.AreEqual(replaceableId, ev.ReplaceableId, "The replacing event should have the same replaceableId");

            eventDatabase.SaveNewEvent(ev, relay);

            // Nothing should have changed

            {
                using var db = eventDatabase.CreateContext();
                Assert.AreEqual(0, db.Events.Count(e => e.Id == id));
                Assert.AreEqual(1, db.Events.Count(e => e.ReplaceableId == replaceableId && e.Id == id2));
                Assert.AreEqual(0, db.Events.Count(e => e.Id == id3));
            }

            // Create a fourth event with date after second event

            nostrev = new NostrEvent()
            {
                Content = "Test4",
                Kind = 11111,
                Tags = new(), // No d, shouldn't matter
                CreatedAt = DateTimeOffset.UtcNow.AddDays(1),
                PublicKey = pubkey,
            };
            nostrev.Id = nostrev.ComputeId();
            nostrev.Signature = TestUtils.GetRandomNostrId(); // Signature doesn't matter
            ev = nostrev.FromNostrEvent();
            var id4 = ev.Id;

            Assert.AreEqual(replaceableId, ev.ReplaceableId, "The replacing event should have the same replaceableId");

            eventDatabase.SaveNewEvent(ev, relay);

            // Nothing should have changed

            {
                using var db = eventDatabase.CreateContext();
                Assert.AreEqual(0, db.Events.Count(e => e.Id == id));
                Assert.AreEqual(0, db.Events.Count(e => e.Id == id2));
                Assert.AreEqual(0, db.Events.Count(e => e.Id == id3));
                Assert.AreEqual(1, db.Events.Count(e => e.ReplaceableId == replaceableId && e.Id == id4));
            }
        }
    }
}