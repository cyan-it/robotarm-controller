using System.Device.I2c;
using Iot.Device.Pwm;

public class ServoManager : IDisposable
{
    private readonly I2cDevice _i2cDevice;
    private readonly Pca9685 _pca9685;

    public ServoManager()
    {
        var settings = new I2cConnectionSettings(1, Pca9685.I2cAddressBase);
        _i2cDevice = I2cDevice.Create(settings);
        _pca9685 = new Pca9685(_i2cDevice, 50);
    }

    public Servo ConnectServo(int channel)
    {
        return new Servo(_pca9685, channel);
    }

    public void Dispose()
    {
        _pca9685?.Dispose();
        _i2cDevice?.Dispose();
    }
}
