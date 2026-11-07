import * as React from "react";

const FieldContext = React.createContext<string | undefined>(undefined);

/** Associates one visible field label with its control, including repeated rows. */
export const FieldScope = React.forwardRef<HTMLDivElement, React.ComponentPropsWithoutRef<"div">>(
  ({ children, ...props }, ref) => {
    const id = React.useId();
    return (
      <FieldContext.Provider value={id}>
        <div ref={ref} {...props}>
          {children}
        </div>
      </FieldContext.Provider>
    );
  },
);
FieldScope.displayName = "FieldScope";

export function useFieldId(explicitId?: string) {
  const scopedId = React.useContext(FieldContext);
  return explicitId ?? scopedId;
}

/** Explicit adapter for a native input, select, or textarea in a field scope. */
export function FieldControl({ children }: { children: React.ReactElement<{ id?: string }> }) {
  const id = useFieldId(children.props.id);
  return React.cloneElement(children, { id });
}
