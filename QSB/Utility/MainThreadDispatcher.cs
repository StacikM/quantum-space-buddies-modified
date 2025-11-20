using System;
using System.Collections.Generic;
using UnityEngine;

namespace QSB.Utility
{
	public class MainThreadDispatcher : MonoBehaviour
	{

		private static MainThreadDispatcher _instance;

		private static readonly Queue<Action> _executionQueue = new Queue<Action>();

		private static readonly object _queueLock = new object();

		public static MainThreadDispatcher Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = FindObjectOfType<MainThreadDispatcher>();

					if (_instance == null)
					{
						var obj = new GameObject("QSBMainThreadDispatcher");
						_instance = obj.AddComponent<MainThreadDispatcher>();
						DontDestroyOnLoad(obj);
					}
				}
				return _instance;
			}
		}

		private void Update()
		{
			lock (_queueLock)
			{
				while (_executionQueue.Count > 0)
				{
					_executionQueue.Dequeue().Invoke();
				}
			}
		}
		public static void RunOnMainThread(Action action)
		{
			lock (_queueLock)
			{
				_executionQueue.Enqueue(action);
			}
		}

		public static void Initialize()
		{
			if (_instance == null)
			{
				var _ = Instance;
			}
		}
	}
}