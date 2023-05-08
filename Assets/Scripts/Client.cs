using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;


public class Client
{
    public static int dataBufferSize = 4096;

    public int id;
    public Player player;
    public TCP tcp;
    public UDP udp;

    public Client(int _clientId)
    {
        id = _clientId;
        tcp = new TCP(id);
        udp = new UDP(id);
    }

    public class TCP
    {
        public TcpClient socket;
        private readonly int id;
        private NetworkStream stream;
        private Packet receiveData;
        private byte[] receiveBuffer;

        public TCP(int _id)
        {
            id = _id;
        }

        public void Connect(TcpClient _socket)
        { 
            socket = _socket;
            socket.ReceiveBufferSize = dataBufferSize;
            socket.SendBufferSize = dataBufferSize;

            //이 네트워크 스트림을 이용해서 네트워크으로 데이타 송수신하게 된다.
            stream = socket.GetStream();

            receiveData = new Packet();
            receiveBuffer = new byte[dataBufferSize];

            ///비동기 읽기 시작
            ///바이트 배열 , offset , NetworkStram에서 읽을수 있는 바이트 수, 비동기 완료시 실행될 콜백 , state Object
            ///읽기가 완료되면 비동기 콜백을 실행
            stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            ServerSend.Welcome(id, "Welcome to the server!");
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                //EndRead -> BeginRead로 시작된 비동기 읽기 작업을 완료
                int _byteLength = stream.EndRead(result);

                if(_byteLength <= 0) { Server.clients[id].Disconnect(); return; }

                byte[] _data = new byte[_byteLength];
                Array.Copy(receiveBuffer,_data, _byteLength);
                receiveData.Reset(HandleData(_data));
                stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            }
            catch(Exception e)
            {
                Debug.Log($"Error receiving TCP data: {e}");
                Server.clients[id].Disconnect();
            }
        }

        private bool HandleData(byte[] _data)
        {
            int _packetLength = 0;

            //데이터 array를 버퍼에 넣기
            receiveData.SetBytes(_data);


            // 버퍼길이 - 현재 읽기 위치
            if (receiveData.UnreadLength() >= 4)
            {
                //현재 받은 배열의 총길이
                _packetLength = receiveData.ReadInt();
                if(_packetLength <= 0)
                {
                    // If packet contains no data
                    // 만약 패킷을 다 읽었다면 true를 반환 재사용
                    return true;
                }                               
            }

            /*
                while문을 이용한 패킷 리드, 
                패킷 길이가 0보다 크고 , 패킷크기가 같거나
                UnreadLength로 남은 패킷의 크기를 확인
                ReadInt로 패킷의 readPos를 갱신
                ThreadManager에 패킷을 읽은 후 Server의 packetHandlers에 packetid와 관련 ServerHandler를 추가한다
                예를들어 _packetId가 2이면 PlayerMovement 이다.
                PlayerMovement의 경우 해당 id의 플레이어에 SetInput을 적용, 그이후 프레임 단위로 움직이게 된다.
                그후 FixedUpdate에서 ServerSend.pos , rot을 통해 전송
                SendUDPDataToAll으로 나머지 전부에 전송
                전 플레이어에게 UDP로 전송
                 */

            while (_packetLength > 0 && _packetLength <= receiveData.UnreadLength())
            {                
                byte[] _packetBytes = receiveData.ReadBytes(_packetLength);
                ThreadManager.ExecuteOnMainThread(() =>
                {
                    using (Packet _packet = new Packet(_packetBytes))
                    {
                        int _packetId = _packet.ReadInt();
                        Server.packetHandlers[_packetId](id, _packet); // Call appropriate method to handle the packet
                    }
                });

                _packetLength = 0;
                if (receiveData.UnreadLength() >= 4)
                {
                    // If client's received data contains another packet
                    _packetLength = receiveData.ReadInt();
                    if (_packetLength <= 0)
                    {
                        //데이터 남았을시
                        return true; // Reset receivedData instance to allow it to be reused
                    }
                }
            }

            if (_packetLength <= 1)
            {
                return true;
            }

            return false;

        }

        public void SendData(Packet _packet)
        {
            try
            {
                if(socket != null)
                {
                    //스트림에 비동기 쓰기 시작
                    //byte배열 , 위치 , Netstram크기 , 비동기 콜백 , state
                    stream.BeginWrite(_packet.ToArray(), 0, _packet.Length(), null, null);
                }
            }
            catch(Exception e)
            {
                Debug.Log($"Error sending data to player {id} via TCP: {e}");
            }
        }
        public void Disconnect()
        {
            socket.Close();
            stream = null;
            receiveData = null;
            receiveBuffer = null;
            socket = null;
        }
    }

    public class UDP
    {
        public IPEndPoint endPoint;
        private int id;

        public UDP(int _id)
        {
            id = _id;
        }

        public void Connect(IPEndPoint _endPoint)
        {
            endPoint = _endPoint;
        }

        public void SendData(Packet _packet)
        {
            Server.SendUDPData(endPoint, _packet);
        }
        public void HandleData(Packet _packetData)
        {
            int _packetLength = _packetData.ReadInt();
            byte[] _packetBytes = _packetData.ReadBytes(_packetLength);

            ThreadManager.ExecuteOnMainThread(() =>
            {
                //사용후 자동반환
                using (Packet _packet = new Packet(_packetBytes))
                {
                    int _packetId = _packet.ReadInt();
                    Server.packetHandlers[_packetId](id, _packet); // Call appropriate method to handle the packet
                }
            });
        }

        public void Disconnect()
        {
            endPoint = null;
        }

    }

    //입장시 플레이어 생성, 등록, 전달
    public void SendIntoGame(string _playerName)
    {
        player = NetworkManager.instance.InstantiatePlayer();
        player.Initialize(id, _playerName);

        foreach (Client _client in Server.clients.Values)
        {
            if (_client.player != null)
            {
                //같은아이디가 아닌 나머지 플레이어에게 전달
                if (_client.id != id)
                {
                    ServerSend.SpawnPlayer(id, _client.player);
                }
            }
        }

        // 자기자신포함
        //위는 기존의 플레이어들에게 내가 왔다는것을 전달,
        //아래는 내가 왔으니 기존의 플레이어들을 내게 전달
        foreach (Client _client in Server.clients.Values)
        {
            if (_client.player != null)
            {
                ServerSend.SpawnPlayer(_client.id, player);
            }
        }

        //초기 생성시 아이템 스폰
        foreach (ItemSpawner _itemSpawner in ItemSpawner.spawners.Values)
        {
            ServerSend.CreateItemSpawner(id, _itemSpawner.spawnerId, _itemSpawner.transform.position, _itemSpawner.hasItem);
        }
    }

    private void Disconnect()
    {
        Debug.Log($"{tcp.socket.Client.RemoteEndPoint} has disconnected.");

        ThreadManager.ExecuteOnMainThread(() =>
        {
            UnityEngine.Object.Destroy(player.gameObject);
            player = null;
        });

        tcp.Disconnect();
        udp.Disconnect();

        ServerSend.PlayerDisconnected(id);
    }

}
