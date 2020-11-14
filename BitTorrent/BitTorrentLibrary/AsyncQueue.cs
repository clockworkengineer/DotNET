//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: ASync usable FIFO queue.
//
// Copyright 2020.
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace BitTorrentLibrary
{

    internal class AsyncQueue<T>
    {
        private readonly SemaphoreSlim _queueSemaphore;
        private readonly ConcurrentQueue<T> _queue;
        public UInt32 Count => (UInt32)_queue.Count;

        /// <summary>
        /// Initialise
        /// </summary>
        public AsyncQueue()
        {
            _queueSemaphore = new SemaphoreSlim(0);
            _queue = new ConcurrentQueue<T>();
        }
        /// <summary>
        /// Place item into back of queue.
        /// </summary>
        /// <param name="item"></param>
        public void Enqueue(T item)
        {
            _queue.Enqueue(item);
            _queueSemaphore.Release();
        }
        /// <summary>
        /// Place number of items into back of queue.
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
        /// Remove item from front of queue (awaiting until an element arrives).
        /// </summary>
        /// <returns></returns>
        public async Task<T> DequeueAsync(CancellationToken cancellationToken = default)
        {
            while (true)
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
