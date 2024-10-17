using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf.preview;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnitTests.PropCacheTest
{
    public class PropCacheTest
    {
        [UnityTest]
        public IEnumerator TestCacheInvalidation()
        {
            int seq = 0;

            Dictionary<int, List<WeakReference<ComputeContext>>> invalidators = new();
            PropCache<int,int> cache = new PropCache<int, int>("test", (ctx, k) =>
            {
                Debug.Log("Generating value for " + k);
                if (!invalidators.TryGetValue(k, out var list))
                {
                    list = new List<WeakReference<ComputeContext>>();
                    invalidators[k] = list;
                }
                
                list.Add(new WeakReference<ComputeContext>(ctx));
                
                return (k * 10) + seq++;
            });
            
            ComputeContext ctx = new ComputeContext("c1");
            int val = cache.Get(ctx, 1);
            Assert.AreEqual(10, val);
            
            ComputeContext ctx2 = new ComputeContext("c2");
            val = cache.Get(ctx2, 1);
            Assert.AreEqual(10, val);

            invalidators[1][0].TryGetTarget(out var target);
            target?.Invalidate();

            Debug.Log("Pre-flush");
            ComputeContext.FlushInvalidates();
            Debug.Log("Mid-flush");
            ComputeContext.FlushInvalidates();
            Debug.Log("Post-flush");

            // Task processing can happen asynchronously.

            int limit = 10;
            while (limit-- > 0 && (!ctx.IsInvalidated || !ctx2.IsInvalidated))
            {
                Debug.Log("Waiting for invalidation: " + limit);
                Thread.Sleep(100);
            }
            
            Assert.IsTrue(ctx.IsInvalidated);
            Assert.IsTrue(ctx2.IsInvalidated);
            
            val = cache.Get(ctx, 1);
            Assert.AreEqual(12, val);

            yield return null;
        }
    }
}