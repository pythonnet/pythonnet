import os
import unittest
import clr

# Import required .NET namespaces
clr.AddReference("System")
clr.AddReference("System.IO")
from System import IDisposable
from System.IO import MemoryStream, FileStream, FileMode, File, Path, StreamWriter

class DisposableContextManagerTests(unittest.TestCase):
    """Tests for Python's context manager protocol with .NET IDisposable objects"""
    
    def test_memory_stream_context_manager(self):
        """Test that MemoryStream can be used as a context manager"""
        data = bytes([1, 2, 3, 4, 5])
        
        # Using with statement with MemoryStream
        with MemoryStream() as stream:
            # Convert Python bytes to .NET byte array for proper writing
            from System import Array, Byte
            dotnet_bytes = Array[Byte](data)
            stream.Write(dotnet_bytes, 0, len(dotnet_bytes))
            
            self.assertEqual(5, stream.Length)
            stream.Position = 0
            
            # Create a .NET byte array to read into
            buffer = Array[Byte](5)
            stream.Read(buffer, 0, 5)
            
            # Convert back to Python bytes for comparison
            result = bytes(buffer)
            self.assertEqual(data, result)
        
        # The stream should be disposed (closed) after the with block
        with self.assertRaises(Exception):
            stream.Position = 0  # This should fail because the stream is closed
    
    def test_file_stream_context_manager(self):
        """Test that FileStream can be used as a context manager"""
        # Create a temporary file path
        temp_path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
        
        try:
            # Write data to the file using with statement
            data = "Hello, context manager!"
            with FileStream(temp_path, FileMode.Create) as fs:
                writer = StreamWriter(fs)
                writer.Write(data)
                writer.Flush()
            
            # Verify the file was written and stream was closed
            self.assertTrue(File.Exists(temp_path))
            content = File.ReadAllText(temp_path)
            self.assertEqual(data, content)
            
            # The stream should be disposed after the with block
            with self.assertRaises(Exception):
                fs.Position = 0  # This should fail because the stream is closed
        finally:
            # Clean up
            if File.Exists(temp_path):
                File.Delete(temp_path)
    
    def test_disposable_in_multiple_contexts(self):
        """Test that using .NET IDisposable objects in multiple contexts works correctly"""
        # Create multiple streams and check that they're all properly disposed
        
        # Create a list to track if streams were properly disposed 
        # (we'll check this by trying to access the stream after disposal)
        streams_disposed = [False, False]
        
        # Use nested context managers with .NET IDisposable objects
        with MemoryStream() as outer_stream:
            # Write some data to the outer stream
            from System import Array, Byte
            outer_data = Array[Byte]([10, 20, 30])
            outer_stream.Write(outer_data, 0, len(outer_data))
            
            # Check that the outer stream is usable
            self.assertEqual(3, outer_stream.Length)
            
            with MemoryStream() as inner_stream:
                # Write different data to the inner stream
                inner_data = Array[Byte]([40, 50, 60, 70])
                inner_stream.Write(inner_data, 0, len(inner_data))
                
                # Check that the inner stream is usable
                self.assertEqual(4, inner_stream.Length)
            
            # Try to use the inner stream - should fail because it's disposed
            try:
                inner_stream.Position = 0
            except Exception:
                streams_disposed[1] = True
        
        # Try to use the outer stream - should fail because it's disposed
        try:
            outer_stream.Position = 0
        except Exception:
            streams_disposed[0] = True
        
        # Verify both streams were properly disposed
        self.assertTrue(all(streams_disposed))
    
    def test_exception_handling(self):
        """Test that exceptions propagate correctly through the context manager"""
        with self.assertRaises(ValueError):
            with MemoryStream() as stream:
                raise ValueError("Test exception")
        
        # Stream should be disposed despite the exception
        with self.assertRaises(Exception):
            stream.Position = 0

if __name__ == "__main__":
    unittest.main()