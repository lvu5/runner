using UnityEngine;
using UnityEngine.SceneManagement;

namespace InfiniteRunner
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : MonoBehaviour
    {
        public enum RunState
        {
            Running,
            GameOver
        }

        private const string BestScoreKey = "InfiniteRunner.BestScore";

        public static GameManager Instance { get; private set; }

        public RunState State { get; private set; } = RunState.Running;
        public RunnerController Player { get; private set; }
        public float DistanceMeters { get; private set; }
        public float RunTime { get; private set; }
        public int Coins { get; private set; }
        public int BestScore { get; private set; }

        public bool IsRunning => State == RunState.Running;
        public int Score => Mathf.FloorToInt(DistanceMeters) + Coins * 5;
        public float TargetRunSpeed => IsRunning
            ? Mathf.Min(maximumSpeed, startingSpeed + RunTime * speedIncreasePerSecond)
            : 0f;
        public float CurrentSpeed => IsRunning && Player != null ? Player.CurrentForwardSpeed : 0f;
        public float Difficulty01 => Mathf.InverseLerp(startingSpeed, maximumSpeed, TargetRunSpeed);

        [Header("Speed")]
        [SerializeField] private float startingSpeed = 8f;
        [SerializeField] private float maximumSpeed = 17f;
        [SerializeField] private float speedIncreasePerSecond = 0.14f;

        private float startingZ;
        private bool initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
            Time.timeScale = 1f;
        }

        public void Configure(RunnerController player)
        {
            Player = player;
            startingZ = player.transform.position.z;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            if (IsRunning)
            {
                if (Player.IsMovingForward)
                {
                    RunTime += Time.deltaTime;
                }

                DistanceMeters = Mathf.Max(0f, Player.transform.position.z - startingZ);
                return;
            }

            if (RunnerInput.RestartPressed())
            {
                Restart();
            }
        }

        public void CollectCoin()
        {
            if (IsRunning)
            {
                Coins++;
            }
        }

        public void EndRun()
        {
            if (!IsRunning)
            {
                return;
            }

            State = RunState.GameOver;
            BestScore = Mathf.Max(BestScore, Score);
            PlayerPrefs.SetInt(BestScoreKey, BestScore);
            PlayerPrefs.Save();
        }

        private static void Restart()
        {
            Time.timeScale = 1f;
            Scene scene = SceneManager.GetActiveScene();

            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);
            }
            else
            {
                SceneManager.LoadScene(scene.name);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
