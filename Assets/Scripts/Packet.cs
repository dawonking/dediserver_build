using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum ServerPackets
{
    welcome = 1,
    spawnPlayer,
    playerPosition,
    playerRotation,
    playerDisconnected,
    playerHealth,
    playerRespawned,
    createItemSpawner,
    itemSpawned,
    itemPickedUp,
    spawnProjectile,
    projectilePosition,
    projectileExploded,
    //
    GameTime,
    GameStart,
    GameEnd,
    GameResult,
}

public enum ClientPackets
{
    welcomeReceived = 1,
    playerMovement,
    playerShoot,
    playerThrowItem
}

public class Packet : IDisposable
{
    private List<byte> buffer;
    private byte[] readableBuffer;
    private int readPos;
    public Packet()
    {
        buffer = new List<byte>();
        readPos = 0;
    }

    public Packet(int _id)
    {
        buffer = new List<byte>();
        readPos = 0;

        Write(_id);
    }

    public Packet(byte[] _data)
    {
        buffer = new List<byte>();
        readPos = 0; // Set readPos to 0

        SetBytes(_data);
    }

    public void SetBytes(byte[] _data)
    {
        Write(_data);

        //버퍼사이즈 만큼 
        readableBuffer = buffer.ToArray();
    }

    #region value    
    public void WriteLength()
    {
        buffer.InsertRange(0, BitConverter.GetBytes(buffer.Count));
    }

    public byte[] ToArray()
    {
        readableBuffer = buffer.ToArray();
        return readableBuffer;
    }

    public int Length()
    {
        return buffer.Count; // Return the length of buffer
    }

    public int UnreadLength()
    {
        //현재 버퍼 길이 - 현재 읽기 위치
        return Length() - readPos; 
    }

    public void Reset(bool _shouldReset = true)
    {
        if (_shouldReset)
        {
            buffer.Clear();
            readableBuffer = null;
            readPos = 0;
        }
        else
        {
            readPos -= 4;
        }
    }
    #endregion


    #region write
    public void Write(bool _value)
    {
        buffer.AddRange(BitConverter.GetBytes(_value));
    }

    public void Write(int _value)
    {
        buffer.AddRange(BitConverter.GetBytes(_value));
    }

    public void Write(float _value)
    {
        buffer.AddRange(BitConverter.GetBytes(_value));
    }

    public void Write(byte[] _value)
    {
        buffer.AddRange(_value);
    }

    public void Write(string _value)
    {
        Write(_value.Length); // Add the length of the string to the packet
        buffer.AddRange(Encoding.ASCII.GetBytes(_value)); // Add the string itself
    }
    public void Write(Vector3 _value)
    {
        Write(_value.x);
        Write(_value.y);
        Write(_value.z);
    }
    public void Write(Quaternion _value)
    {
        Write(_value.x);
        Write(_value.y);
        Write(_value.z);
        Write(_value.w);
    }

    #endregion

    #region ReadByte

    public byte[] ReadBytes(int _length, bool _moveReadPos = true)
    {
        if (buffer.Count > readPos)
        {            
            byte[] _value = buffer.GetRange(readPos, _length).ToArray();
            if (_moveReadPos)
            {                
                readPos += _length;
            }
            return _value; // Return the bytes
        }
        else
        {
            throw new Exception("Could not read value of type 'byte[]'!");
        }
    }

    public int ReadInt(bool _moveReadPos = true)
    {
        //남은 버퍼 총량이 읽기 위치보다 크면
        if (buffer.Count > readPos)
        {
            //읽기 위치부터 4바이트를 읽어서 int로 변환 , 읽기 위치 이동
            int _value = BitConverter.ToInt32(readableBuffer, readPos);
            if (_moveReadPos)
            {                
                readPos += 4;
            }
            //전송받은 패킷의 종류 확인
            return _value;
        }
        else
        {
            throw new Exception("Could not read value of type 'int'!");
        }
    }

    public float ReadFloat(bool _moveReadPos = true)
    {
        if (buffer.Count > readPos)
        {
            float _value = BitConverter.ToSingle(readableBuffer, readPos);
            if (_moveReadPos)
            {                
                readPos += 4;
            }
            return _value;
        }
        else
        {
            throw new Exception("Could not read value of type 'float'!");
        }
    }

    public string ReadString(bool _moveReadPos = true)
    {
        try
        {
            int _length = ReadInt(); 
            string _value = Encoding.ASCII.GetString(readableBuffer, readPos, _length);
            if (_moveReadPos && _value.Length > 0)
            {                
                readPos += _length;
            }
            return _value;
        }
        catch
        {
            throw new Exception("Could not read value of type 'string'!");
        }
    }

    public bool ReadBool(bool _moveReadPos = true)
    {
        if (buffer.Count > readPos)
        {
            bool _value = BitConverter.ToBoolean(readableBuffer, readPos);
            if (_moveReadPos)
            {                
                readPos += 1; 
            }
            return _value;
        }
        else
        {
            throw new Exception("Could not read value of type 'bool'!");
        }
    }

    public Vector3 ReadVector3(bool _moveReadPos = true)
    {
        return new Vector3(ReadFloat(_moveReadPos), ReadFloat(_moveReadPos), ReadFloat(_moveReadPos));
    }

    public Quaternion ReadQuaternion(bool _moveReadPos = true)
    {
        return new Quaternion(ReadFloat(_moveReadPos), ReadFloat(_moveReadPos), ReadFloat(_moveReadPos), ReadFloat(_moveReadPos));
    }

    #endregion


    private bool disposed = false;

    protected virtual void Dispose(bool _disposing)
    {
        if (!disposed)
        {
            if (_disposing)
            {
                buffer = null;
                readableBuffer = null;
                readPos = 0;
            }

            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

}
