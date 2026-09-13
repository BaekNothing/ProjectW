using System;
using NUnit.Framework;
using ProjectW.Contracts;
using UnityEngine;

namespace ProjectW.WebPreview.Tests
{
    public sealed class WebPreviewBootstrapTests
    {
        private sealed class Entry : IGameEntry
        {
            public int Calls;
            public GameStartupContext Context;
            public void Start(GameStartupContext context) { Calls++; Context = context; context.MarkHealthy(); }
        }

        [Test]
        public void StartsStaticEntryOnceAndReportsHealthy()
        {
            var host = new GameObject("web-test");
            try
            {
                var bootstrap = host.AddComponent<WebPreviewBootstrap>();
                var entry = new Entry();
                bootstrap.StartGame(entry);
                bootstrap.StartGame(entry);
                Assert.That(entry.Calls, Is.EqualTo(1));
                Assert.That(entry.Context.DataPath, Is.Empty);
                Assert.That(bootstrap.Status, Is.EqualTo("실행 중"));
                Assert.That(bootstrap.RequestUpdate(), Is.False);
                Assert.That(bootstrap.UpdateInProgress, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void RejectsMissingEntryWithoutConsumingStartup()
        {
            var host = new GameObject("web-test");
            try
            {
                var bootstrap = host.AddComponent<WebPreviewBootstrap>();
                Assert.Throws<ArgumentNullException>(() => bootstrap.StartGame(null));
                var entry = new Entry();
                bootstrap.StartGame(entry);
                Assert.That(entry.Calls, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
    }
}
