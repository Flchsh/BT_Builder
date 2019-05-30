using UnityEngine;
using System.Collections;
using BTBuilder;

public class deb : MonoBehaviour {

    BT_Behave aimanager;
    Vector3 testeer = new Vector3(0, 0, 0);
	// Use this for initialization
	void Start () {
        aimanager = this.gameObject.GetComponent<BT_Behave>();
        aimanager.blackBoard.Set<Vector3>(testeer, "newpos");
	}
	
	// Update is called once per frame
	void Update () {
        this.gameObject.transform.position = aimanager.blackBoard.Get<Vector3>("newpos");
	}
}
