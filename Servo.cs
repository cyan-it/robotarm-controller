using System.Device.Pwm;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Iot.Device.Pwm;
using Iot.Device.ServoMotor;
using UnitsNet;

public class Servo : IDisposable
{
    private const int MaxRotation = 180;

    private readonly PwmChannel _pwmChannel;
    private readonly ServoMotor _servoMotor;

    private readonly BehaviorSubject<int?> _angleChanges;

    private readonly IDisposable _angleSubscription;
    private int _queuedAngle;
    private int _angle;
    public int Angle => _angle;

    public Servo(Pca9685 pca9685, int channel)
    {        
        _pwmChannel = pca9685.CreatePwmChannel(channel);

        _servoMotor = new ServoMotor(_pwmChannel, MaxRotation, 700, 2400);
        _servoMotor.Start();

        _angleChanges = new BehaviorSubject<int?>(null);

        _angleSubscription = _angleChanges
            .Where(angle => angle is not null)
            .Select(angle => angle!.Value)
            .Sample(TimeSpan.FromMilliseconds(30))
            .Subscribe(MoveTo);

        QueueMoveTo(MaxRotation / 2);
    }

    private Timer? _movementTimer = null;
    private decimal _movementSpeed;
    private ServoDirection _movementDirection;

    public void BeginOrContinueMovement(decimal speed, ServoDirection direction)
    {
        _movementSpeed = speed;
        _movementDirection = direction;
        _movementTimer ??= new Timer(_ => ExecuteMovement(), null, 0, 30);
    }

    private void ExecuteMovement()
    {
        var angle = SafeAngle(_queuedAngle + (int)(10 * _movementSpeed) * (_movementDirection == ServoDirection.Left ? -1 : 1));
        QueueMoveTo(angle);
    }

    public void EndMovement()
    {
        QueueMoveTo(_angle);

        if(_movementTimer is null)
        {
            return;
        }

        _movementTimer.Dispose();
        _movementTimer = null;
    }

    public void QueueMoveTo(int angle)
    {
        _queuedAngle = angle;
        _angleChanges.OnNext(angle);
    }

    private void MoveTo(int angle)
    {
        angle = SafeAngle(angle);

        _angle = angle;
        _servoMotor.WriteAngle(angle);
    }

    private int SafeAngle(int angle)
    {
        angle = Math.Min(170, angle);
        angle = Math.Max(10, angle);
        return angle;
    }

    public void Dispose()
    {
        _movementTimer?.Dispose();
        _angleSubscription?.Dispose();
        _angleChanges?.Dispose();
        _servoMotor?.Stop();
        _servoMotor?.Dispose();
        _pwmChannel?.Dispose();
    }
}