//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Perform HTTP announce requests to remote tracker.
//
// Copyright 2020.
//

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BitTorrentLibrary
{
    public class AsyncQueue<T>
    {
        private readonly SemaphoreSlim _queueSemaphore;
        private readonly ConcurrentQueue<T> _queue;
        /// <summary>
        /// 
        /// </summary>
        public AsyncQueue()
        {
            _queueSemaphore = new SemaphoreSlim(0);
            _queue = new ConcurrentQueue<T>();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        public void Enqueue(T item)
        {
            _queue.Enqueue(item);
            _queueSemaphore.Release();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        public void EnqueueRange(IEnumerable<T> source)
        {
            var numberOfItems = 0;
            foreach (var item in source)
            {
                _queue.Enqueue(item);
                numberOfItems++;
            }
            _queueSemaphore.Release(numberOfItems);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<T> DequeueAsync(CancellationToken cancellationToken = default)
        {
            for (; ; )
            {
                await _queueSemaphore.WaitAsync(cancellationToken);

                if (_queue.TryDequeue(out T item))
                {
                    return item;
                }
            }
        }
    }
}
