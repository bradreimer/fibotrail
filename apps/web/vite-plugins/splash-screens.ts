import type { Plugin } from 'vite';

export interface SplashScreen {
  /** The screen size in CSS points, in portrait. */
  width: number;
  height: number;
  ratio: number;
  /** Tablets also get a landscape image. */
  landscape?: boolean;
}

/** The link tags that tell iOS which launch image fits the device. */
export function splashLinks(screens: readonly SplashScreen[]): string {
  return screens
    .flatMap((screen) => {
      const orientations = screen.landscape ? ['portrait', 'landscape'] : ['portrait'];
      return orientations.map((orientation) => {
        const [w, h] =
          orientation === 'portrait'
            ? [screen.width * screen.ratio, screen.height * screen.ratio]
            : [screen.height * screen.ratio, screen.width * screen.ratio];
        const media =
          `(device-width: ${screen.width}px) and (device-height: ${screen.height}px) and ` +
          `(-webkit-device-pixel-ratio: ${screen.ratio}) and (orientation: ${orientation})`;
        return `<link rel="apple-touch-startup-image" media="${media}" href="/splash/splash-${w}x${h}.png" />`;
      });
    })
    .join('\n    ');
}

/** Puts the launch image links into index.html, in place of the splash comment. */
export function splashScreens(screens: readonly SplashScreen[]): Plugin {
  return {
    name: 'fibotrail-splash-screens',
    transformIndexHtml: (html) => html.replace('<!--splash-->', splashLinks(screens)),
  };
}
