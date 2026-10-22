using System;
using System.Drawing;

namespace DildoCursor
{
    /// <summary>
    /// Cuerpo de goma: una cadena de nodos unidos por resortes angulares, anclada en la
    /// punta (que sigue al mouse de forma rigida para que el puntero apunte bien).
    ///
    /// De aca sale todo el movimiento: al mover el mouse los nodos se quedan atras por
    /// inercia y el cuerpo se curva y rebota; la gravedad lo hace colgar solo (no hay
    /// ninguna curva dibujada a mano); y la rigidez sube cuando se pone duro, asi que
    /// endurecerse lo endereza y lo deja de bambolear como efecto secundario.
    /// </summary>
    internal sealed class RubberBody
    {
        public readonly PointF[] P;     // posiciones, en coordenadas de pantalla
        readonly PointF[] V;            // velocidades, px/s

        const float MaxStep = 1f / 180f;   // paso fijo: con resortes duros Euler explicito
                                           // se vuelve inestable si el frame es largo

        public RubberBody(int nodes)
        {
            P = new PointF[nodes];
            V = new PointF[nodes];
        }

        public void Reset(PointF anchor, float angleRad, float seg)
        {
            float dx = (float)Math.Cos(angleRad) * seg;
            float dy = (float)Math.Sin(angleRad) * seg;
            for (int i = 0; i < P.Length; i++)
            {
                P[i] = new PointF(anchor.X + dx * i, anchor.Y + dy * i);
                V[i] = PointF.Empty;
            }
        }

        public void Step(float dt, PointF anchor, float angleRad, float seg,
                         float stiffness, float damping, float gravity, float maxBendRad)
        {
            int steps = (int)Math.Ceiling(dt / MaxStep);
            if (steps < 1) steps = 1;
            if (steps > 24) steps = 24;
            float h = dt / steps;

            float dx = (float)Math.Cos(angleRad) * seg;
            float dy = (float)Math.Sin(angleRad) * seg;
            float damp = (float)Math.Exp(-damping * h);

            for (int s = 0; s < steps; s++)
            {
                P[0] = anchor;
                V[0] = PointF.Empty;

                for (int i = 1; i < P.Length; i++)
                {
                    // El reposo de cada nodo es "el anterior, corrido en la direccion base":
                    // asi la gravedad se acumula nodo a nodo y el cuerpo se curva de verdad
                    // en vez de trasladarse entero.
                    float ax = (P[i - 1].X + dx - P[i].X) * stiffness;
                    float ay = (P[i - 1].Y + dy - P[i].Y) * stiffness + gravity;

                    // El tramo no se estira, asi que de la fuerza solo sobrevive lo
                    // transversal: lo que tira a lo largo se lo come la tension. Si no la
                    // sacamos, el resorte estira el tramo, el constraint lo corta, el
                    // resorte vuelve a estirar... y el cuerpo nunca termina de frenar.
                    float sx = P[i].X - P[i - 1].X, sy = P[i].Y - P[i - 1].Y;
                    float sl = (float)Math.Sqrt(sx * sx + sy * sy);
                    if (sl > 0.0001f)
                    {
                        sx /= sl; sy /= sl;
                        float radial = ax * sx + ay * sy;
                        ax -= sx * radial; ay -= sy * radial;
                    }

                    float vx = (V[i].X + ax * h) * damp;
                    float vy = (V[i].Y + ay * h) * damp;
                    V[i] = new PointF(vx, vy);
                    P[i] = new PointF(P[i].X + vx * h, P[i].Y + vy * h);
                }

                // La goma se dobla pero no se estira: fijamos el largo de cada tramo
                // propagando desde el ancla.
                for (int it = 0; it < 2; it++)
                {
                    for (int i = 1; i < P.Length; i++)
                    {
                        float ddx = P[i].X - P[i - 1].X;
                        float ddy = P[i].Y - P[i - 1].Y;
                        float len = (float)Math.Sqrt(ddx * ddx + ddy * ddy);
                        if (len < 0.0001f) { ddx = dx; ddy = dy; len = seg; }
                        float k = seg / len;
                        P[i] = new PointF(P[i - 1].X + ddx * k, P[i - 1].Y + ddy * k);
                    }
                }

                // Una goma se dobla, no se pliega: limitamos cuanto puede quebrarse de un
                // tramo al siguiente. Sin esto, con el cuerpo colgando de la base la cadena
                // se enrolla sobre si misma y el dibujo se hace un bollo.
                if (maxBendRad > 0f)
                {
                    // Arranca en i=1, y el primer tramo se mide contra el angulo de reposo.
                    // Si no, el cuerpo entero puede girar sobre su base sin "doblarse" en
                    // ningun lado, y por mas rigido que lo pongas sigue pareciendo goma.
                    for (int i = 1; i < P.Length; i++)
                    {
                        float aAng = i == 1
                            ? angleRad
                            : (float)Math.Atan2(P[i - 1].Y - P[i - 2].Y, P[i - 1].X - P[i - 2].X);
                        float bAng = (float)Math.Atan2(P[i].Y - P[i - 1].Y, P[i].X - P[i - 1].X);

                        float d = bAng - aAng;
                        while (d > Math.PI) d -= (float)(2 * Math.PI);
                        while (d < -Math.PI) d += (float)(2 * Math.PI);

                        if (d > maxBendRad) d = maxBendRad;
                        else if (d < -maxBendRad) d = -maxBendRad;
                        else continue;

                        float na = aAng + d;
                        P[i] = new PointF(P[i - 1].X + (float)Math.Cos(na) * seg,
                                          P[i - 1].Y + (float)Math.Sin(na) * seg);
                    }
                }

                // Los constraints de arriba corrigen posiciones a mano, pero la velocidad
                // sigue apuntando a estirar el tramo: el resorte vuelve a estirarlo, el
                // constraint lo vuelve a cortar, y asi el cuerpo nunca termina de frenar.
                //
                // Le sacamos entonces a cada nodo la parte de su velocidad que va en la
                // direccion del tramo (la que estira o comprime). Es la correccion de
                // velocidad que acompaña a una restriccion rigida, y disipa en vez de
                // inventar energia. Ojo: rehacer la velocidad entera como (P - previa)/h
                // NO sirve aca, porque divide la correccion por el sub-paso y la amplifica.
                for (int i = 1; i < P.Length; i++)
                {
                    float dx2 = P[i].X - P[i - 1].X, dy2 = P[i].Y - P[i - 1].Y;
                    float l2 = (float)Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                    if (l2 < 0.0001f) continue;
                    dx2 /= l2; dy2 /= l2;

                    float along = (V[i].X - V[i - 1].X) * dx2 + (V[i].Y - V[i - 1].Y) * dy2;
                    V[i] = new PointF(V[i].X - dx2 * along, V[i].Y - dy2 * along);
                }
            }
        }

        /// <summary>
        /// Arrastra el cuerpo hacia su forma de reposo (la de <paramref name="rest"/>, que
        /// esta anclado en el origen). Se usa cuando el mouse ya esta quieto y el rebote
        /// practicamente termino, para que se asiente del todo en vez de vibrar.
        /// </summary>
        public void PullToRest(RubberBody rest, PointF anchor, float t)
        {
            if (t <= 0f) return;
            if (t > 1f) t = 1f;

            for (int i = 0; i < P.Length; i++)
            {
                float tx = anchor.X + rest.P[i].X;
                float ty = anchor.Y + rest.P[i].Y;
                P[i] = new PointF(P[i].X + (tx - P[i].X) * t, P[i].Y + (ty - P[i].Y) * t);
                V[i] = new PointF(V[i].X * (1f - t), V[i].Y * (1f - t));
            }
        }

        /// <summary>Que tan quieto esta (para poder saltear el repintado).</summary>
        public float MaxSpeed()
        {
            float m = 0f;
            for (int i = 1; i < V.Length; i++)
            {
                float s = Math.Abs(V[i].X) + Math.Abs(V[i].Y);
                if (s > m) m = s;
            }
            return m;
        }
    }
}
