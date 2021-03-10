using System;
using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts
{
	public class CircularBuffer<T>
	{
		private readonly Queue<T> _queue;
		private readonly int _capacity;

		public CircularBuffer(int capacity)
		{
			_queue = new Queue<T>(capacity);
			_capacity = capacity;
		}

		public T Read()
		{
			if (IsEmpty())
			{
				//throw new InvalidOperationException("Cannot read from an empty buffer");
			}

			return _queue.Dequeue();
		}

		public void Write(T value)
		{
			if (_queue.Count == _capacity)
			{
				//throw new InvalidOperationException("Cannot write to a full buffer.");
			}

			_queue.Enqueue(value);
		}

		public bool IsEmpty()
        {
			return !_queue.Any();
        }

		public void Clear()
		{
			_queue.Clear();
		}
	}
}