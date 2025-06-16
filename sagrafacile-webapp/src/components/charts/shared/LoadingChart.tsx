import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

interface LoadingChartProps {
    height?: number;
}

export function LoadingChart({ height = 300 }: LoadingChartProps) {
    return (
        <Card>
            <CardHeader>
                <CardTitle>
                    <Skeleton className="h-6 w-48" />
                </CardTitle>
            </CardHeader>
            <CardContent>
                <div className="space-y-3">
                    <Skeleton className="h-4 w-full" />
                    <Skeleton className="h-4 w-3/4" />
                    <Skeleton className="h-4 w-1/2" />
                    <div className="mt-4">
                        <Skeleton className={`w-full`} style={{ height: `${height}px` }} />
                    </div>
                </div>
            </CardContent>
        </Card>
    );
}
