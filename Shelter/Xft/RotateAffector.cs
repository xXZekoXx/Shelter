using UnityEngine;

namespace Xft
{
    public class RotateAffector : Affector
    {
        protected float Delta;
        protected AnimationCurve RotateCurve;
        protected RSTYPE Type;

        public RotateAffector(float delta, EffectNode node) : base(node)
        {
            this.Type = RSTYPE.SIMPLE;
            this.Delta = delta;
        }

        public RotateAffector(AnimationCurve curve, EffectNode node) : base(node)
        {
            this.Type = RSTYPE.CURVE;
            this.RotateCurve = curve;
        }

        public override void Update()
        {
            float elapsedTime = Node.GetElapsedTime();
            if (this.Type == RSTYPE.CURVE)
            {
                Node.RotateAngle = (int) this.RotateCurve.Evaluate(elapsedTime);
            }
            else if (this.Type == RSTYPE.SIMPLE)
            {
                float num2 = Node.RotateAngle + this.Delta * Time.deltaTime;
                Node.RotateAngle = num2;
            }
        }
    }
}

