using UnityEngine;

namespace InfiniteRunner
{
    public sealed class CoinPickup : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed = 145f;

        private GameManager gameManager;

        public void Configure(GameManager manager)
        {
            gameManager = manager;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!gameObject.activeSelf || gameManager == null || !gameManager.IsRunning)
            {
                return;
            }

            if (other.GetComponent<RunnerController>() == null)
            {
                return;
            }

            gameManager.CollectCoin();
            gameObject.SetActive(false);
        }
    }
}
