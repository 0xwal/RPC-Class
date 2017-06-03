using System;
using System.Linq;

class RPCEventArgs : EventArgs
{
    public RPCEventArgs(object returnValue, uint functionAddress)
    {
        ReturnValue = returnValue;
        FunctionAddress = functionAddress;
    }
    public object ReturnValue { get; private set; }
    public uint FunctionAddress { get; private set; }
}

class RPC
{
    public event EventHandler<RPCEventArgs> IsFunctionCallFinished;
    private static PS3API PS3;
    private uint _parametersPointer = 0x10070000;
    private uint _callerFunctionAddress;
    private uint? _branchFrom;
    private uint h_params_size = 0x100;
    private void _Enable()
    {
        //Inject the ppc code into an hookable address.
        if (IsEnabled)
            return;
        _DestroyAll();
        _parametersPointer = _parametersPointer & 0xFFFF0000;
        uint _16bit = _parametersPointer >> 16;
        byte firstByte = (byte)(_16bit >> 8);
        byte secondByte = (byte)(_16bit & 0x00FF);
        #region ppcOpcode
        byte[] ppc_code = {
            0xF8, 0x21, 0xFF, 0x91,  //stdu %r1, -0x70(%r1)
            0x7C, 0x08, 0x02, 0xA6,  //mfspr %r0, %lr
            0xF8, 0x01, 0x00, 0x80,  //std %r0, 0x80(%r1)
            0x3C, 0x60, firstByte, secondByte,  //lis %r3, _parametersPointer
            0x81, 0x83, 0x00, 0x4C,  //lwz %r12, 0x4C(%r3)
            0x2C, 0x0C, 0x00, 0x00,  //cmpwi %r12, 0
            0x41, 0x82, 0x00, 0x64,  //beq 0x64
            0x80, 0x83, 0x00, 0x04,  //lwz %r4, 0x4(%r3)
            0x80, 0xA3, 0x00, 0x08,  //lwz %r5, 0x8(%r3)
            0x80, 0xC3, 0x00, 0x0C,  //lwz %r6, 0xC(%r3)
            0x80, 0xE3, 0x00, 0x10,  //lwz %r7, 0x10(%r3)
            0x81, 0x03, 0x00, 0x14,  //lwz %r8, 0x14(%r3)
            0x81, 0x23, 0x00, 0x18,  //lwz %r9, 0x18(%r3)
            0x81, 0x43, 0x00, 0x1C,  //lwz %r10, 0x1C(%r3)
            0x81, 0x63, 0x00, 0x20,  //lwz %r11, 0x20(%r3)
            0xC0, 0x23, 0x00, 0x24,  //lfs %f1, 0x24(%r3)
            0xC0, 0x43, 0x00, 0x28,  //lfs %f2, 0x28(%r3)
            0xC0, 0x63, 0x00, 0x2C,  //lfs %f3, 0x2C(%r3)
            0xC0, 0x83, 0x00, 0x30,  //lfs %f4, 0x30(%r3)
            0xC0, 0xA3, 0x00, 0x34,  //lfs %f5, 0x34(%r3)
            0xC0, 0xC3, 0x00, 0x38,  //lfs %f6, 0x38(%r3)
            0xC0, 0xE3, 0x00, 0x3C,  //lfs %f7, 0x3C(%r3)
            0xC1, 0x03, 0x00, 0x40,  //lfs %f8, 0x40(%r3)
            0xC1, 0x23, 0x00, 0x44,  //lfs %f9, 0x44(%r3)
            0x80, 0x63, 0x00, 0x00,  //lwz %r3, 0x0(%r3)
            0x7D, 0x89, 0x03, 0xA6,  //mtctr %r12
            0x4E, 0x80, 0x04, 0x21,  //bctrl
            0x3C, 0x80, 0x10, 0x07,  //lis %r4, 0x1007
            0x38, 0xA0, 0x00, 0x00,  //li %r5, 0x00
            0x90, 0xA4, 0x00, 0x4C,  //stw %r5, 0x4C(%r4)
            0x90, 0x64, 0x00, 0x50,  //stw %r3, 0x50(%r4)
            0xE8, 0x01, 0x00, 0x80,  //ld %r0, 0x80(%r1)
            0x7C, 0x08, 0x03, 0xA6,  //mtspr %lr, %r0
            0x38, 0x21, 0x00, 0x70,  //addi %r1, %r1, 0x70
            0x4E, 0x80, 0x00, 0x20 };//blr 
        #endregion
        PS3.SetMemory(_callerFunctionAddress, ppc_code);
        if (_branchFrom != null)
        {
            uint branchFrom = (uint)_branchFrom;
            PS3.Extension.WriteUInt32(branchFrom, _Branch(branchFrom, _callerFunctionAddress));
        }
    }
    private uint _Branch(uint branchFrom, uint branchTo)
    {
        uint branch;
        if (branchTo > branchFrom)
            branch = 0x48000001 + (branchTo - branchFrom);
        else
            branch = 0x4C000001 - (branchFrom - branchTo);
        return branch;
    }
    private void _DestroyAll()
    {
        PS3.SetMemory(_parametersPointer, new byte[0x54]);
        PS3.SetMemory(_parametersPointer + h_params_size, new byte[400]);
    }
    public RPC(PS3API ps3, uint callerFunctionAddress, uint? branchFrom = null)
    {
        PS3 = ps3;
        _callerFunctionAddress = callerFunctionAddress;
        _branchFrom = branchFrom;
        _Enable();
    }
    public bool IsEnabled
    {
        get
        {
            if (PS3.GetBytes(_callerFunctionAddress, 4).SequenceEqual(new byte[] { 0xF8, 0x21, 0xFF, 0x91 }))
                return true;
            return false;
        }
    }
    private void OnCall(object retValue, uint functionAddress)
    {
        if (IsFunctionCallFinished != null)
        {
            IsFunctionCallFinished(this, new RPCEventArgs(retValue, functionAddress));
        }
    }
    public  T Call<T>(uint address, params object[] parameters)  where T :   struct, IEquatable<T>
    {
        uint paramsCount = (uint)parameters.Length;
        if (paramsCount > 17)
        {
            throw new ArgumentException("arguments length (" + paramsCount + ") more than the allowed length 17!");
        }
        uint nonPrimitiveValuesPointer = _parametersPointer + h_params_size;
        uint functionAddress = _parametersPointer + 0x4C;
        uint r3ReturnValue = _parametersPointer + 0x50;
        uint floatIndex = 0;
        object retValue = null;
        uint temp = 0;
        _DestroyAll();
        for (uint i = 0; i < paramsCount; i++)
        {
            object currentParamater = parameters[i];
            if (currentParamater.GetType().IsPrimitive)
            {
                if (currentParamater is int)
                {
                    PS3.Extension.WriteInt32(_parametersPointer + 4 * (i - temp), (int)currentParamater);
                    continue;
                }
                if (currentParamater is uint)
                {
                    PS3.Extension.WriteUInt32(_parametersPointer + 4 * (i - temp), (uint)currentParamater);
                    continue;
                }
                if (currentParamater is bool)
                {
                    PS3.Extension.WriteUInt32(_parametersPointer + 4 * (i - temp), Convert.ToUInt32(currentParamater));
                    continue;
                }
                if(currentParamater is float)
                {
                    PS3.Extension.WriteFloat((_parametersPointer + 0x24) + 4 * floatIndex, Convert.ToSingle(currentParamater));
                    floatIndex++;
                    temp++;
                    continue;
                }
                
            }
            else
            {
                if (currentParamater is float[])
                {
                    uint sizeOfCurrentNonprimitveValue = (uint)((float[])currentParamater).Length;
                    PS3.Extension.WriteFloat(nonPrimitiveValuesPointer, (float[])currentParamater);
                    PS3.Extension.WriteUInt32(((_parametersPointer + 0x24) + 4 * floatIndex), nonPrimitiveValuesPointer);
                    nonPrimitiveValuesPointer = nonPrimitiveValuesPointer + (sizeOfCurrentNonprimitveValue * 4);
                    floatIndex++;
                    temp++;
                    continue;
                }
                if (currentParamater is string)
                {
                    uint sizeOfCurrentNonprimitveValue = (uint)((string)currentParamater).Length + 1;
                    PS3.Extension.WriteString(nonPrimitiveValuesPointer, (string)currentParamater + '\0');
                    PS3.Extension.WriteUInt32(_parametersPointer + (4 * (i - temp)), nonPrimitiveValuesPointer);
                    nonPrimitiveValuesPointer = (nonPrimitiveValuesPointer + sizeOfCurrentNonprimitveValue);
                }
            }
        }
        PS3.Extension.WriteUInt32(functionAddress, address);
        DateTime dt = DateTime.Now.AddSeconds(10);
        bool inUse = true;
        while ((inUse = (PS3.Extension.ReadInt32(functionAddress) != 0)) && dt > DateTime.Now)
        {}
        if (inUse)
        {
            PS3.Extension.WriteUInt32(functionAddress, 0);
        }
        retValue = PS3.Extension.ReadInt32(r3ReturnValue);
        retValue = Convert.ChangeType(retValue, typeof(T));
        OnCall(retValue, address);
        return (T)retValue;
    }
}
