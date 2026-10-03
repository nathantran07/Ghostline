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
        [SerializeField] private Vector2 _spawnPosition = new Vector2(-2f, -6f);
        [SerializeField] private float _spawnRotation = -90f;
        private RaceSession _session;
        private BestLapRepository _repository;
        private BestLapData _bestLap;
        private bool _saveFailed;

        public void Configure(CarController car, GhostCarView ghost, HudView hud, CameraFollow cameraFollow)
        {
            _car = car;
            _ghost = ghost;
            _hud = hud;
            _cameraFollow = cameraFollow;
        }

        private void Start()
        {
            if (_car == null || _ghost == null || _hud == null)
            {
                Debug.LogError("Ghostline RaceManager needs Car, Ghost, and HUD references.", this);
                enabled = false;
                return;
            }
            _session = new RaceSession(4);
            _repository = new BestLapRepository(new JsonFileBestLapStorage());
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
        }

        private void FixedUpdate()
        {
            if (_session == null)
                return;
            _session.Tick(Time.fixedDeltaTime, _car.Body.position.x, _car.Body.position.y, _car.Body.rotation);
        }

        public void CrossTrigger(CarController car, bool isStartFinish, int checkpointIndex)
        {
            if (_session == null || car != _car)
                return;
            if (!isStartFinish)
            {
                _session.PassCheckpoint(checkpointIndex);
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
            _saveFailed = false;
            _car.ResetPose(_spawnPosition, _spawnRotation);
            _ghost.SetLap(_bestLap);
            if (_cameraFollow != null)
                _cameraFollow.SnapToTarget();
            _hud.Render(_session, _bestLap, _saveFailed);
        }
    }
}
