// Loads the SCS web component from its (potentially cross-origin) URL and
// waits until the custom element is registered.
let loadPromise: Promise<void> | null = null;

export function loadComponent(url: string): Promise<void> {
  loadPromise ??= new Promise<void>((resolve, reject) => {
    const script = document.createElement('script');
    script.type = 'module';
    script.src = url;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error(`Failed to load SCS component from ${url}`));
    document.head.appendChild(script);
  });
  return loadPromise;
}
