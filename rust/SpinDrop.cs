using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SpinDrop", "Fessnecro", "0.1.1", ResourceId = 2554)]
    [Description("Spin around dropped weapons and tools above the ground")]
    public class SpinDrop : RustPlugin
    {
        private void OnItemDropped(Item item, BaseEntity entity)
        {
            var category = item.info.category.ToString();
            if (category == "Weapon" || category == "Tool")
            {
                var gameObject = item.GetWorldEntity().gameObject;
                var rigidBody = gameObject.GetComponent<Rigidbody>();
                rigidBody.useGravity = false;
                rigidBody.isKinematic = true;
                gameObject.transform.position = new Vector3(gameObject.transform.position.x, gameObject.transform.position.y - 1f, gameObject.transform.position.z);
                gameObject.AddComponent<SpinDropControl>();
            }
        }

        public class SpinDropControl : MonoBehaviour
        {
            public int speed = 100;

            private void Update()
            {
                gameObject.transform.Rotate(Vector3.down * Time.deltaTime * speed);
            }
        }
    }
}
