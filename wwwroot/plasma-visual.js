const PLASMA_VERSION = "20261013-plasma-motion";

async function mountPlasmaVisual() {
  const host = document.getElementById("plasmaLayer");
  if (!host) return;

  try {
    const [{ default: React }, reactDom, plasma] = await Promise.all([
      import("https://esm.sh/react@18.3.1"),
      import("https://esm.sh/react-dom@18.3.1/client"),
      import("https://esm.sh/@cruxgarden/plasma-ui@0.8.0?bundle&external=react,react-dom")
    ]);

    const { createElement: h } = React;
    const { createRoot } = reactDom;
    const { PlasmaProvider, Plasma } = plasma;
    const compact = window.matchMedia("(max-width: 900px)").matches;

    const Surface = ({ className, style, children }) => h(
      Plasma,
      {
        className: `plasma-decoration ${className}`,
        fuse: false,
        draggable: false,
        lean: false,
        radius: compact ? 18 : 28,
        opacity: compact ? 0.22 : 0.32,
        frost: compact ? 0.28 : 0.48,
        elevation: 0.44,
        refraction: 0.9,
        dispersion: 0.3,
        rim: 0.7,
        shimmer: 0.85,
        formIn: true,
        formSpeed: 0.8,
        shimmerSpeed: 1.15,
        style,
        children
      }
    );

    const app = h(
      PlasmaProvider,
      {
        mood: "tidal",
        theme: "dark",
        tint: "#6bcfe2",
        opacity: compact ? 0.24 : 0.36,
        frost: compact ? 0.22 : 0.38,
        viscosity: 0.52,
        flow: 0.34,
        stretch: compact ? 0.35 : 0.65,
        glow: 0.9,
        wash: 0.62,
        grain: 0.12,
        shimmer: 0.85,
        shimmerSpeed: 1.25,
        rim: 0.75,
        edgeLine: 0.7,
        backgroundBlur: compact ? 1 : 3,
        formIn: true,
        formSpeed: 0.8,
        quality: compact ? 0.7 : 1,
        maxSurfaces: compact ? 3 : 5,
        freezeOnScroll: compact,
        ambientDrops: !compact,
        pointerDrop: true,
        pointerPull: true,
        children: [
          h(Surface, { key: "a", className: "plasma-decoration-a", style: { left: "8%", top: "16%", width: compact ? "42%" : "25%", height: compact ? "14%" : "18%" } }),
          h(Surface, { key: "b", className: "plasma-decoration-b", style: { right: "9%", top: "30%", width: compact ? "34%" : "22%", height: compact ? "17%" : "23%" } }),
          h(Surface, { key: "c", className: "plasma-decoration-c", style: { left: "24%", bottom: "12%", width: compact ? "48%" : "30%", height: compact ? "15%" : "19%" } })
        ]
      }
    );

    createRoot(host).render(app);
    document.documentElement.dataset.plasma = PLASMA_VERSION;
  } catch (error) {
    host.remove();
    document.documentElement.dataset.plasma = "fallback";
    console.warn("Plasma UI indisponível; usando o visual CSS compatível.", error);
  }
}

mountPlasmaVisual();
