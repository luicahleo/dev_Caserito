/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_CASERITO_ENVIRONMENT?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
