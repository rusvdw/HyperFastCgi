using System;
using System.Threading;

namespace Mono.WebServer.HyperFastCgi.FastCgiProtocol
{
	public class RecordsManager
	{
		private const int cacheSize = 128;
		private static RecordsManager manager = new RecordsManager ();
		private static Record[] records;
		private static int[] freeRecordsStack;
		private static int stackPointer;
		private static object stackLock = new object ();
		private bool useManager = true;

		public static RecordsManager Instance {
			get { return manager; }
		}

		private RecordsManager ()
		{
			records = new Record[cacheSize];
			freeRecordsStack = new int[cacheSize];
			stackPointer = 0;
			//TODO: init array
			for (int i = 0; i < records.Length; i++) {
				records [i] = new Record ();
				records [i].Body = new byte[65536];
				freeRecordsStack [i] = i;
			}
		}

		public Record GetRecord (byte version, RecordType type, ushort requestID,
		                        byte[] bodyData, int bodyIndex, int bodyLength)
		{
			byte paddingLength = 0;

			//workaround nginx error. It does not support paddingLength if bodyLength==0 
//			if (bodyLength > 0) {
//				paddingLength = (byte)((Record.HeaderSize + bodyLength) & 0x0F);
//				if (paddingLength > 0)
//					paddingLength = (byte)(16 - paddingLength);
//			}

			return GetRecord (version, type, requestID, bodyData, bodyIndex, bodyLength, paddingLength);
		}

		public Record GetRecord (byte version, RecordType type, ushort requestID,
		                        byte[] bodyData, int bodyIndex, int bodyLength, byte paddingLength)
		{
			int idx = -1;
			Record record;

			if (useManager && stackPointer < cacheSize) {
				lock (stackLock) {
					if (stackPointer < cacheSize) {
						idx = freeRecordsStack [stackPointer];
						stackPointer++;
					}
				}
			}

			if (idx >= 0) {
				record = records [idx];
				record.CacheIndex = idx;
				record.Version = version;
				record.Type = type;
				record.RequestId = requestID;
				record.BodyOffset = 0;
				record.BodyLength = (ushort)bodyLength;
				if (bodyData != null) {
					Buffer.BlockCopy (bodyData, bodyIndex, record.Body, 0, bodyLength);
				}
			} else {
				record = new Record (version, type, requestID, bodyData, 0, bodyLength);
				if (bodyData != null) {
					Buffer.BlockCopy (bodyData, bodyIndex, record.Body, 0, bodyLength);
				}
			}
			record.PaddingLength = paddingLength;

			return record;
		}

		public void ReleaseRecord (Record record)
		{
			if (useManager && record.CacheIndex >= 0) {
				lock (stackLock) {
					stackPointer--;
					freeRecordsStack [stackPointer] = record.CacheIndex;
				}
			}
		}
	}
}

