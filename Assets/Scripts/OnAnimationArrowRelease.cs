using UnityEngine;

public class OnAnimationArrowRelease : StateMachineBehaviour
{
    [SerializeField] private AnimationEvent myEvent;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateinfo, int layerIndex)
    {
        if (myEvent != null)
        {
            ProjectileLaunch _script = animator.GetComponentInParent<ProjectileLaunch>();
        }
    }
}
