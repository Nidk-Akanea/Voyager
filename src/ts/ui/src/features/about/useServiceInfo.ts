// Reads the diagnostics of the local service (proto/voyager/diagnostics/v1/diagnostics.proto).
import { useQuery } from '@tanstack/react-query';

import { useAppContext } from '../../app/appContext';

export function useServiceInfo() {
  const { clients } = useAppContext();
  return useQuery({
    queryKey: ['diagnostics', 'service-info'],
    queryFn: ({ signal }) => clients.diagnostics.getServiceInfo({}, { signal }),
  });
}
