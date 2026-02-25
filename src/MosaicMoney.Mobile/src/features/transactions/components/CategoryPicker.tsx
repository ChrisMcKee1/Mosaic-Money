import { useState, useEffect, useCallback } from "react";
import {
  View,
  Text,
  TextInput,
  StyleSheet,
  FlatList,
  Pressable,
  ActivityIndicator,
} from "react-native";
import { searchCategories } from "../services/mobileTransactionsApi";
import type { CategorySearchResultDto } from "../contracts";
import { theme } from "../../../theme/tokens";

interface CategoryPickerProps {
  value?: string;
  onChange: (categoryId?: string) => void;
  placeholder?: string;
}

export function CategoryPicker({ value, onChange, placeholder = "Search categories..." }: CategoryPickerProps) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<CategorySearchResultDto[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState<CategorySearchResultDto | null>(null);
  const [isOpen, setIsOpen] = useState(false);

  useEffect(() => {
    const normalizedQuery = query.trim();
    if (!normalizedQuery) {
      setResults([]);
      setIsSearching(false);
      return;
    }

    setIsSearching(true);
    const abortController = new AbortController();

    const timeoutId = setTimeout(async () => {
      try {
        const data = await searchCategories(normalizedQuery, 10, abortController.signal);
        setResults(data);
      } catch (e) {
        if (e instanceof Error && e.name === "AbortError") return;
        console.error("Category search failed", e);
        setResults([]);
      } finally {
        setIsSearching(false);
      }
    }, 300);

    return () => {
      clearTimeout(timeoutId);
      abortController.abort();
    };
  }, [query]);

  const handleSelect = useCallback((cat: CategorySearchResultDto) => {
    setSelectedCategory(cat);
    setQuery("");
    setIsOpen(false);
    onChange(cat.id);
  }, [onChange]);

  const handleClear = useCallback(() => {
    setSelectedCategory(null);
    onChange(undefined);
  }, [onChange]);

  return (
    <View style={styles.container}>
      <View style={styles.inputContainer}>
        {selectedCategory ? (
          <View style={styles.selectedContainer}>
            <Text style={styles.selectedText}>
              {selectedCategory.categoryName} {">"} {selectedCategory.name}
            </Text>
            <Pressable onPress={handleClear} style={styles.clearButton}>
              <Text style={styles.clearButtonText}>x</Text>
            </Pressable>
          </View>
        ) : (
          <TextInput
            style={styles.input}
            placeholder={placeholder}
            placeholderTextColor={theme.colors.textMuted}
            value={query}
            onChangeText={(text) => {
              setQuery(text);
              setIsOpen(true);
            }}
            onFocus={() => setIsOpen(true)}
          />
        )}
        {isSearching && (
          <ActivityIndicator size="small" color={theme.colors.primary} style={styles.spinner} />
        )}
      </View>

      {isOpen && query.trim().length > 0 && (
        <View style={styles.dropdown}>
          {results.length === 0 && !isSearching ? (
            <Text style={styles.noResults}>No categories found.</Text>
          ) : (
            <FlatList
              data={results}
              keyExtractor={(item) => item.id}
              keyboardShouldPersistTaps="handled"
              renderItem={({ item }) => (
                <Pressable
                  style={[
                    styles.resultItem,
                    value === item.id && styles.resultItemSelected,
                  ]}
                  onPress={() => handleSelect(item)}
                >
                  <Text
                    style={[
                      styles.resultItemText,
                      value === item.id && styles.resultItemTextSelected,
                    ]}
                  >
                    {item.categoryName} {">"} {item.name}
                  </Text>
                </Pressable>
              )}
              style={styles.list}
            />
          )}
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    position: "relative",
    zIndex: 1,
    marginBottom: 16,
  },
  inputContainer: {
    flexDirection: "row",
    alignItems: "center",
    backgroundColor: theme.colors.background,
    borderWidth: 1,
    borderColor: theme.colors.border,
    borderRadius: 8,
    minHeight: 44,
  },
  input: {
    flex: 1,
    paddingHorizontal: 12,
    paddingVertical: 10,
    color: theme.colors.textMain,
    fontSize: 16,
  },
  selectedContainer: {
    flex: 1,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    paddingHorizontal: 12,
    paddingVertical: 10,
  },
  selectedText: {
    color: theme.colors.textMain,
    fontSize: 16,
  },
  clearButton: {
    padding: 4,
  },
  clearButtonText: {
    color: theme.colors.textMuted,
    fontSize: 16,
  },
  spinner: {
    position: "absolute",
    right: 12,
  },
  dropdown: {
    position: "absolute",
    top: "100%",
    left: 0,
    right: 0,
    backgroundColor: theme.colors.surface,
    borderWidth: 1,
    borderColor: theme.colors.border,
    borderRadius: 8,
    marginTop: 4,
    maxHeight: 200,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
    zIndex: 10,
  },
  list: {
    flexGrow: 0,
  },
  resultItem: {
    paddingHorizontal: 12,
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: theme.colors.border,
  },
  resultItemSelected: {
    backgroundColor: theme.colors.primary + "1A", // 10% opacity
  },
  resultItemText: {
    color: theme.colors.textMain,
    fontSize: 16,
  },
  resultItemTextSelected: {
    color: theme.colors.primary,
    fontWeight: "500",
  },
  noResults: {
    padding: 12,
    color: theme.colors.textMuted,
    textAlign: "center",
  },
});