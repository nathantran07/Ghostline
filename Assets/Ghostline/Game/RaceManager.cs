using System;
using System.IO;
using System.Security;
using Ghostline.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ghostline.Game
{
    // Tick on the physics clock; trigger callbacks accept crossings after each simulation step.
    [DefaultExecutionOrder(100)]
    public sealed class RaceManager : MonoBehaviour
    {
        [SerializeField] private CarController _car;
        [SerializeField] private GhostCarView _ghost;
        [SerializeField] private HudView _hud;
        [SerializeField] private CameraFollow _cameraFollow;
        [SerializeField, Min(1)] private int _checkpointCount = 12;
        [SerializeField] private string _trackId = BestLapData.DefaultTrackId;
        [SerializeField] private Vector2 _spawnPosition = new Vector2(-2f, -6f);
        [SerializeField] private float _spawnRotation = -90f;
        private RaceSession _session;
        private BestLapRepository _repository;
        private BestLapData _bestLap;
        private bool _saveFailed;
        private readonly StartSequence _startSequence = new StartSequence();

        public int CheckpointCount => _session == null ? _checkpointCount : _session.Checkpoints.CheckpointCount;

        public bool ShowGhostOnMinimap => _session != null && _ghost != null && _ghost.isActiveAndEnabled
            && _ghost.HasRecording && (_session.Timer.State == LapTimerState.NotStarted
                || (_session.Timer.State == LapTimerState.Running && _session.Timer.ElapsedTime < _ghost.PlaybackDuration));

        public Vector2 GhostMinimapPosition => _session != null && _session.Timer.State == LapTimerState.NotStarted
            ? _spawnPosition : (_ghost == null ? _spawnPosition : (Vector2)_ghost.transform.position);

        public void SetSpawn(Vector2 position, float rotation)
        {
            _spawnPosition = position;
            _spawnRotation = rotation;
            if (Application.isPlaying)
                return;
            Quaternion orientation = Quaternion.Euler(0f, 0f, rotation);
            _car?.transform.SetPositionAndRotation(position, orientation);
            _ghost?.transform.SetPositionAndRotation(position, orientation);
            _cameraFollow?.SnapToTarget();
        }

        public void Configure(CarController car, GhostCarView ghost, HudView hud, CameraFollow cameraFollow,
            int checkpointCount = 12, Vector2? spawnPosition = null, float spawnRotation = -90f)
        {
            if (checkpointCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(checkpointCount));
            _car = car;
            _ghost = ghost;
            _hud = hud;
            _cameraFollow = cameraFollow;
            _checkpointCount = checkpointCount;
            if (spawnPosition.HasValue)
                _spawnPosition = spawnPosition.Value;
            _spawnRotation = spawnRotation;
        }

        private void Start()
        {
            if (_car == null || _ghost == null || _hud == null)
            {
                Debug.LogError("Ghostline RaceManager needs Car, Ghost, and HUD references.", this);
                enabled = false;
                return;
            }
            if (_checkpointCount <= 0 || string.IsNullOrWhiteSpace(_trackId))
            {
                Debug.LogError("Ghostline RaceManager needs a positive checkpoint count and track identity.", this);
                enabled = false;
                return;
            }
            _session = new RaceSession(_checkpointCount, trackId: _trackId);
            _repository = new BestLapRepository(new JsonFileBestLapStorage(_checkpointCount, _trackId), _checkpointCount, _trackId);
            _bestLap = _repository.Load();
            Restart();
        }

        private void Update()
        {
            if (_session == null)
                return;
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                Restart();
            if (_session.Timer.State == LapTimerState.Running)
                _ghost.ShowAt(_session.Timer.ElapsedTime);
            _hud.Render(_session, _bestLap, _saveFailed);
            _hud.RenderCountdown(_startSequence);
        }

        private void FixedUpdate()
        {
            if (_session == null)
                return;
            _startSequence.Tick(Time.fixedDeltaTime);
            _car.InputEnabled = _startSequence.DrivingAllowed;
            _hud.Tick(Time.fixedDeltaTime);
            _hud.RenderCountdown(_startSequence);
            _session.Tick(Time.fixedDeltaTime, _car.Body.position.x, _car.Body.position.y, _car.Body.rotation);
        }

        public void CrossTrigger(CarController car, bool isStartFinish, int checkpointIndex)
        {
            if (_session == null || car != _car || !_startSequence.DrivingAllowed)
                return;
            if (!isStartFinish)
            {
                if (_session.PassCheckpoint(checkpointIndex))
                    _hud.ShowDelta(DeltaCalculator.AtCheckpoint(_bestLap, checkpointIndex,
                        _session.Splits[checkpointIndex]));
                return;
            }
            if (!_session.CrossStartFinish(car.Body.position.x, car.Body.position.y, car.Body.rotation))
                return;
            if (_session.Timer.State == LapTimerState.Running)
            {
                _ghost.ShowAt(0f);
                return;
            }
            car.CanDrive = false;
            _hud.ShowDelta(DeltaCalculator.AtFinish(_bestLap, _session.CompletedLap.LapTime));
            try
            {
                if (_repository.TrySave(_session.CompletedLap))
                    _bestLap = _session.CompletedLap;
            }
            catch (Exception exception) when (exception is IOException
                || exception is UnauthorizedAccessException || exception is SecurityException)
            {
                _saveFailed = true;
                Debug.LogWarning($"Ghostline could not save its best lap: {exception.Message}", this);
            }
        }

        public void Restart()
        {
            if (_session == null)
                return;
            _session.Reset();
            _startSequence.Reset();
            _saveFailed = false;
            _car.ResetPose(_spawnPosition, _spawnRotation);
            _car.InputEnabled = false;
            _ghost.SetLap(_bestLap);
            if (_cameraFollow != null)
                _cameraFollow.SnapToTarget();
            _hud.Render(_session, _bestLap, _saveFailed);
            _hud.RenderCountdown(_startSequence);
            _hud.ShowDelta(null);
        }
    }
}
