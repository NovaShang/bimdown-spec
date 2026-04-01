export interface RawField {
  name: string;
  type: string;
  required?: boolean;
  computed?: boolean;
  reference?: string;
  values?: string[];
  description?: string;
}

export interface RawSchema {
  name: string;
  abstract?: boolean;
  bases?: string[];
  host_type?: string;
  description?: string;
  fields: RawField[];
}

export interface ResolvedField {
  name: string;
  type: string;
  required: boolean;
  computed: boolean;
  reference?: string;
  values?: string[];
  description?: string;
}

export interface ResolvedTable {
  name: string;
  prefix: string;
  hostType?: string;
  allFields: ResolvedField[];
  csvFields: ResolvedField[];
  computedFields: ResolvedField[];
  description?: string;
}
